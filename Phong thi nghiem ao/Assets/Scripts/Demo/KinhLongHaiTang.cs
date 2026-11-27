using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Khkt.Rendering;

namespace Khkt.Demo
{
    /// <summary>
    /// Quy ước Layer 14 dành riêng cho tầng Kính Điều Hướng (Navigation Layer) ngoài Phòng Lab.
    /// Phòng Lab vẫn nằm nguyên trên layer UI (5) và không dùng hệ 2 tầng này.
    /// </summary>
    public static class KinhLongLop
    {
        public const int LayerMacDinh = 14;
        public const string TenLayerDieuHuong = "KinhDieuHuong";
        static int _layer = -1;

        public static int LayerDieuHuong
        {
            get
            {
                if (_layer < 0)
                {
                    int tim = LayerMask.NameToLayer(TenLayerDieuHuong);
                    _layer = tim >= 0 ? tim : LayerMacDinh;
                }
                return _layer;
            }
        }

        public static void DatLopDeQuy(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                DatLopDeQuy(t.GetChild(i).gameObject, layer);
        }
    }

    /// <summary>
    /// Gắn trên Base Camera của màn ngoài Phòng Lab để tách 2 tầng trên CÙNG một RenderGraph (không cần camera phụ):
    ///   - Camera vẫn giữ Layer 14 (KinhDieuHuong) trong cullingMask để Unity chuẩn bị mesh/layout cho cả 2 Canvas.
    ///   - Renderer URP (PC_Renderer / Mobile_Renderer) bỏ bit 14 khỏi pass vẽ mặc định -> pass vẽ gốc chỉ vẽ Content Layer.
    ///   - KinhLongBlurFeature (AfterRenderingTransparents) chụp _KL_NoiDung + tháp mờ _KL_Mo1..4, sau đó tự vẽ riêng
    ///     RendererList của Layer 14 lên activeColorTexture.
    /// Trong Phòng Lab hoặc khi tắt Kính Lỏng (Classic), Base Camera trả lại cullingMask gốc và Feature không chạy pass nào.
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongCameraNen : MonoBehaviour
    {
        static readonly HashSet<Camera> _camerasHoatDong = new HashSet<Camera>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _camerasHoatDong.Clear();
        }

        Camera _baseCam;
        UniversalAdditionalCameraData _baseUad;
        int _maskGoc;
        bool _daLuuMask;
        bool _dangBat;

        public static bool IsCameraNenActive(Camera cam)
        {
            return cam != null && _camerasHoatDong.Contains(cam);
        }

        public static KinhLongCameraNen Ensure(Camera baseCam, bool active)
        {
            if (baseCam == null) return null;
            var c = baseCam.GetComponent<KinhLongCameraNen>();
            if (!active)
            {
                if (c != null) c.DatKichHoat(false);
                return c;
            }
            if (c == null) c = baseCam.gameObject.AddComponent<KinhLongCameraNen>();
            c.DatKichHoat(true);
            return c;
        }

        void Awake()
        {
            _baseCam = GetComponent<Camera>();
        }

        void OnEnable()
        {
            if (_baseCam == null) _baseCam = GetComponent<Camera>();
            if (_dangBat) CapNhatTrangThai(true);
        }

        void OnDisable()
        {
            CapNhatTrangThai(false);
        }

        void OnDestroy()
        {
            if (_baseCam != null) _camerasHoatDong.Remove(_baseCam);
            if (_baseCam != null && _daLuuMask)
                _baseCam.cullingMask = _maskGoc;
        }

        public void DatKichHoat(bool active)
        {
            _dangBat = active;
            CapNhatTrangThai(active);
        }

        void CapNhatTrangThai(bool active)
        {
            if (_baseCam == null) _baseCam = GetComponent<Camera>();
            if (_baseCam == null) return;

            if (!_daLuuMask)
            {
                _maskGoc = _baseCam.cullingMask;
                _daLuuMask = true;
            }

            if (_baseUad == null) _baseUad = _baseCam.GetUniversalAdditionalCameraData();

            int navBit = 1 << KinhLongLop.LayerDieuHuong;

            if (!active || !GlassKit.LiquidGlass)
            {
                _camerasHoatDong.Remove(_baseCam);
                if (_daLuuMask) _baseCam.cullingMask = _maskGoc;
                return;
            }

            CameraKhongHauKy.Gan(_baseCam);
            _baseUad.renderType = CameraRenderType.Base;
            _baseUad.renderPostProcessing = false;
            _baseCam.cullingMask = _maskGoc | navBit;

            _camerasHoatDong.Add(_baseCam);
            KinhLongBlurCache.DanhDauBan(3);
        }
    }

    /// <summary>
    /// Đồng bộ thông số ScreenSpaceCamera &amp; CanvasScaler của Canvas Điều Hướng theo Canvas Nội Dung chính.
    /// Chỉ ghi khi khác giá trị cũ để tránh đánh dấu bẩn Canvas không cần thiết.
    /// </summary>
    [DisallowMultipleComponent]
    public class DongBoCanvasDieuHuong : MonoBehaviour
    {
        public Canvas CanvasNoiDung;
        public Canvas CanvasDieuHuong;
        public CanvasScaler ScalerNoiDung;
        public CanvasScaler ScalerDieuHuong;

        void LateUpdate() => DongBoNgay();

        public void DongBoNgay()
        {
            if (CanvasNoiDung == null || CanvasDieuHuong == null) return;

            Camera targetCam = CanvasNoiDung.worldCamera != null ? CanvasNoiDung.worldCamera : Camera.main;
            if (CanvasDieuHuong.renderMode != RenderMode.ScreenSpaceCamera)
                CanvasDieuHuong.renderMode = RenderMode.ScreenSpaceCamera;
            if (CanvasDieuHuong.worldCamera != targetCam)
                CanvasDieuHuong.worldCamera = targetCam;
            if (!Mathf.Approximately(CanvasDieuHuong.planeDistance, CanvasNoiDung.planeDistance))
                CanvasDieuHuong.planeDistance = CanvasNoiDung.planeDistance;
            if (CanvasDieuHuong.pixelPerfect != CanvasNoiDung.pixelPerfect)
                CanvasDieuHuong.pixelPerfect = CanvasNoiDung.pixelPerfect;
            if (CanvasDieuHuong.sortingLayerID != CanvasNoiDung.sortingLayerID)
                CanvasDieuHuong.sortingLayerID = CanvasNoiDung.sortingLayerID;
            int targetOrder = CanvasNoiDung.sortingOrder + 50;
            if (!Mathf.Approximately(CanvasDieuHuong.sortingOrder, targetOrder))
                CanvasDieuHuong.sortingOrder = targetOrder;

            var channels = CanvasNoiDung.additionalShaderChannels | AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            if (CanvasNoiDung.additionalShaderChannels != channels)
                CanvasNoiDung.additionalShaderChannels = channels;
            if (CanvasDieuHuong.additionalShaderChannels != channels)
                CanvasDieuHuong.additionalShaderChannels = channels;

            if (ScalerNoiDung != null && ScalerDieuHuong != null)
            {
                if (!ScalerDieuHuong.enabled) ScalerDieuHuong.enabled = true;
                if (ScalerDieuHuong.uiScaleMode != ScalerNoiDung.uiScaleMode)
                    ScalerDieuHuong.uiScaleMode = ScalerNoiDung.uiScaleMode;
                if (ScalerDieuHuong.referenceResolution != ScalerNoiDung.referenceResolution)
                    ScalerDieuHuong.referenceResolution = ScalerNoiDung.referenceResolution;
                if (ScalerDieuHuong.screenMatchMode != ScalerNoiDung.screenMatchMode)
                    ScalerDieuHuong.screenMatchMode = ScalerNoiDung.screenMatchMode;
                if (!Mathf.Approximately(ScalerDieuHuong.matchWidthOrHeight, ScalerNoiDung.matchWidthOrHeight))
                    ScalerDieuHuong.matchWidthOrHeight = ScalerNoiDung.matchWidthOrHeight;
                if (!Mathf.Approximately(ScalerDieuHuong.referencePixelsPerUnit, ScalerNoiDung.referencePixelsPerUnit))
                    ScalerDieuHuong.referencePixelsPerUnit = ScalerNoiDung.referencePixelsPerUnit;
            }
        }
    }

    /// <summary>
    /// Quản lý cặp Hai Tầng Canvas (Canvas Nội Dung + Canvas Điều Hướng) trên cùng một màn hình ngoài Lab.
    /// Luôn giữ Canvas Nội Dung đứng trước trong thứ tự con để GetComponentInChildren&lt;Canvas&gt;()
    /// của các bài kiểm tra (KhktUiRunner) luôn trỏ đúng Canvas gốc.
    /// </summary>
    [DisallowMultipleComponent]
    public class HaiTangCanvas : MonoBehaviour
    {
        public Canvas CanvasNoiDung { get; private set; }
        public Canvas CanvasDieuHuong { get; private set; }
        public KinhLongCameraNen CameraNen { get; private set; }

        DongBoCanvasDieuHuong _dongBo;
        readonly List<KinhGuongDieuHuong> _cacPhanTu = new List<KinhGuongDieuHuong>();

        public static HaiTangCanvas Ensure(Canvas canvasNoiDung, bool active)
        {
            if (canvasNoiDung == null) return null;
            var h = canvasNoiDung.GetComponent<HaiTangCanvas>();
            if (!active)
            {
                if (h != null) h.DatKichHoat(false);
                return h;
            }
            if (h == null) h = canvasNoiDung.gameObject.AddComponent<HaiTangCanvas>();
            h.KhoiTao(canvasNoiDung);
            h.DatKichHoat(true);
            return h;
        }

        void KhoiTao(Canvas noiDung)
        {
            CanvasNoiDung = noiDung;
            if (CanvasDieuHuong == null)
            {
                Transform parent = noiDung.transform.parent;
                GameObject go = null;
                if (parent != null)
                {
                    Transform existing = parent.Find("CanvasDieuHuongKinh");
                    if (existing != null) go = existing.gameObject;
                }
                else
                {
                    var roots = noiDung.gameObject.scene.GetRootGameObjects();
                    for (int i = 0; i < roots.Length; i++)
                    {
                        if (roots[i].name == "CanvasDieuHuongKinh") { go = roots[i]; break; }
                    }
                }

                if (go == null)
                {
                    go = new GameObject("CanvasDieuHuongKinh", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    if (parent != null)
                    {
                        go.transform.SetParent(parent, false);
                        go.transform.SetSiblingIndex(noiDung.transform.GetSiblingIndex() + 1);
                    }
                    else
                    {
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, noiDung.gameObject.scene);
                        go.transform.SetSiblingIndex(noiDung.transform.GetSiblingIndex() + 1);
                    }
                }

                KinhLongLop.DatLopDeQuy(go, KinhLongLop.LayerDieuHuong);
                CanvasDieuHuong = go.GetComponent<Canvas>();
                _dongBo = go.GetComponent<DongBoCanvasDieuHuong>() ?? go.AddComponent<DongBoCanvasDieuHuong>();
                _dongBo.CanvasNoiDung = CanvasNoiDung;
                _dongBo.CanvasDieuHuong = CanvasDieuHuong;
                _dongBo.ScalerNoiDung = CanvasNoiDung.GetComponent<CanvasScaler>();
                _dongBo.ScalerDieuHuong = go.GetComponent<CanvasScaler>();
                _dongBo.DongBoNgay();
            }
        }

        public void DatKichHoat(bool active)
        {
            if (CanvasNoiDung == null) return;
            Camera baseCam = CanvasNoiDung.worldCamera != null ? CanvasNoiDung.worldCamera : Camera.main;
            if (baseCam != null && CanvasNoiDung.renderMode == RenderMode.ScreenSpaceCamera)
                CameraNen = KinhLongCameraNen.Ensure(baseCam, active);
            else
                CameraNen = null;

            if (CanvasDieuHuong != null)
            {
                CanvasDieuHuong.gameObject.SetActive(active && GlassKit.LiquidGlass);
                if (_dongBo != null) _dongBo.DongBoNgay();
            }

            for (int i = _cacPhanTu.Count - 1; i >= 0; i--)
            {
                if (_cacPhanTu[i] == null) { _cacPhanTu.RemoveAt(i); continue; }
                _cacPhanTu[i].ApDung(active && GlassKit.LiquidGlass);
            }
        }

        public void DangKy(KinhGuongDieuHuong elem)
        {
            if (elem != null && !_cacPhanTu.Contains(elem))
                _cacPhanTu.Add(elem);
        }

        public void HuyDangKy(KinhGuongDieuHuong elem)
        {
            _cacPhanTu.Remove(elem);
        }

        void OnDestroy()
        {
            if (CameraNen != null) CameraNen.DatKichHoat(false);
        }
    }

    /// <summary>
    /// Đánh dấu một cụm điều hướng cấp cao (Thanh bên, Thanh nổi, Hộp công cụ, Menu bùng)
    /// để tự động đưa lên Tầng Điều Hướng (CanvasDieuHuong + Layer 14 KinhDieuHuong) khi Kính Lỏng bật,
    /// và trả về nguyên trạng khi chuyển Classic hoặc gỡ bỏ.
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhGuongDieuHuong : MonoBehaviour
    {
        Transform _chaGoc;
        int _viTriConGoc;
        int _layerGoc = 5;
        bool _daLuuGoc;
        HaiTangCanvas _haiTang;
        bool _dangOTangDieuHuong;

        public static KinhGuongDieuHuong Gan(GameObject go, HaiTangCanvas haiTang, bool bat)
        {
            if (go == null) return null;
            var c = go.GetComponent<KinhGuongDieuHuong>() ?? go.AddComponent<KinhGuongDieuHuong>();
            c.GanVao(haiTang);
            c.ApDung(bat && GlassKit.LiquidGlass);
            return c;
        }

        public void GanVao(HaiTangCanvas haiTang)
        {
            if (_haiTang != null && _haiTang != haiTang)
                _haiTang.HuyDangKy(this);
            _haiTang = haiTang;
            if (_haiTang != null) _haiTang.DangKy(this);
        }

        public void ApDung(bool lenDieuHuong)
        {
            if (!_daLuuGoc)
            {
                _chaGoc = transform.parent;
                _viTriConGoc = transform.GetSiblingIndex();
                _layerGoc = gameObject.layer;
                _daLuuGoc = true;
            }

            if (lenDieuHuong && _haiTang != null && _haiTang.CanvasDieuHuong != null && _haiTang.CanvasDieuHuong.gameObject.activeSelf)
            {
                if (transform.parent != _haiTang.CanvasDieuHuong.transform)
                    transform.SetParent(_haiTang.CanvasDieuHuong.transform, false);
                KinhLongLop.DatLopDeQuy(gameObject, KinhLongLop.LayerDieuHuong);
                _dangOTangDieuHuong = true;
                KinhLongBlurCache.DanhDauBan(3);
            }
            else if (_dangOTangDieuHuong)
            {
                if (_chaGoc != null && transform.parent != _chaGoc)
                {
                    transform.SetParent(_chaGoc, false);
                    transform.SetSiblingIndex(Mathf.Min(_viTriConGoc, _chaGoc.childCount - 1));
                }
                KinhLongLop.DatLopDeQuy(gameObject, _layerGoc);
                _dangOTangDieuHuong = false;
                KinhLongBlurCache.DanhDauBan(3);
            }
        }

        void OnDestroy()
        {
            if (_haiTang != null) _haiTang.HuyDangKy(this);
        }
    }
}
