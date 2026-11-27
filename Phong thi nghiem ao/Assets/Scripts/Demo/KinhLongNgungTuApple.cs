using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Hiệu ứng ngưng tụ quang học Apple iOS 26 (0.00s -> 0.40s Hermite):
    /// không fade opacity cả tấm bảng mà đẩy chiết suất 0 -> 1.35x -> 1.0,
    /// tăng mờ 0.25x -> 1.25x -> 1.0 và lóe sáng vành ở ~0.18s rồi lắng lại.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KinhHinh))]
    public class KinhLongNgungTuApple : MonoBehaviour
    {
        KinhHinh _kinh;
        float _thoiGian;
        float _thoiLuong = 0.40f;
        bool _dangChay;
        bool _epThuCong;

        public float TienDo => _epThuCong ? (_kinh != null ? _kinh.NgungTu : 1f) : Mathf.Clamp01(_thoiGian / Mathf.Max(0.05f, _thoiLuong));

        public static KinhLongNgungTuApple Attach(KinhHinh kinh, float thoiLuong = 0.40f)
        {
            if (kinh == null) return null;
            var c = kinh.GetComponent<KinhLongNgungTuApple>() ?? kinh.gameObject.AddComponent<KinhLongNgungTuApple>();
            c._kinh = kinh;
            c.BatDau(thoiLuong);
            return c;
        }

        void OnEnable()
        {
            _kinh = GetComponent<KinhHinh>();
            if (!_epThuCong) BatDau(0.40f);
        }

        public void BatDau(float thoiLuong = 0.40f)
        {
            _epThuCong = false;
            _thoiLuong = ReducedMotion.IsEnabled ? 0.10f : Mathf.Max(0.05f, thoiLuong);
            _thoiGian = 0f;
            _dangChay = true;
            if (_kinh != null)
            {
                _kinh.NgungTu = 0f;
                _kinh.CapNhatVatLieu();
            }
            VungKinhApple.BaoBan(2);
        }

        /// <summary>Đặt trực tiếp tiến độ ngưng tụ t ∈ [0..1] (dùng cho ảnh kiểm chuẩn 0.00s, 0.14s, 0.25s, 0.40s).</summary>
        public void DatTaiThoiDiem(float tNorm)
        {
            _epThuCong = true;
            _dangChay = false;
            if (_kinh == null) _kinh = GetComponent<KinhHinh>();
            if (_kinh != null)
            {
                _kinh.NgungTu = Mathf.Clamp01(tNorm);
                _kinh.CapNhatVatLieu();
            }
            VungKinhApple.BaoBan(2);
        }

        void Update()
        {
            if (!_dangChay || _kinh == null) return;

            _thoiGian += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float x = Mathf.Clamp01(_thoiGian / _thoiLuong);
            // Hermite mượt (0 -> 1) khớp nhịp quang học WWDC25 4c
            float hermite = x * x * (3f - 2f * x);
            _kinh.NgungTu = hermite;
            _kinh.CapNhatVatLieu();
            VungKinhApple.BaoBan(2);

            if (x >= 1f)
            {
                _dangChay = false;
                _kinh.NgungTu = 1f;
                _kinh.CapNhatVatLieu();
            }
        }
    }

    /// <summary>
    /// Chuyển động lỏng Apple cho nút/thẻ ngoài Phòng Lab có gắn KinhHinh:
    ///   - Rọi sáng cục bộ theo con trỏ (hover glow)
    ///   - Sóng chạm theo đúng tọa độ điểm nhấn &amp; lan nhẹ sang vùng chứa
    ///   - Nảy macOS 27 (0.35, 0.55) / Pop iPad 1.15x
    ///   - Kéo gel dùng KinhLong.CaoSu giữ bảo toàn thể tích (sx * sy ≈ 1)
    ///   - Viền sáng focus khi điều hướng bàn phím Tab
    ///   - Giảm chuyển động (ReducedMotion): tắt kéo/nảy, chỉ giữ sáng nhẹ 0.12s
    /// </summary>
    [DisallowMultipleComponent]
    public class NutHieuUngApple : MonoBehaviour,
        IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        ISelectHandler, IDeselectHandler
    {
        public static bool GiaLapIpad;

        KinhHinh _kinh;
        RectTransform _rt;
        Vector3 _scaleGoc = Vector3.one;
        Vector2 _anchorPosGoc;
        bool _daLuuGoc;

        LoXo _springScale = new LoXo(1f);
        LoXo2 _springOffset = new LoXo2(Vector2.zero);
        LoXo2 _springStretch = new LoXo2(Vector2.one);

        bool _hovered;
        bool _pressed;
        bool _focused;
        Vector2 _pointerDownLocal;
        Vector2 _hoverUv = new Vector2(0.5f, 0.5f);
        float _hoverIntensity;
        Vector2 _touchUv = new Vector2(0.5f, 0.5f);
        float _touchIntensity;
        float _touchRadius = 60f;
        Vector2 _dragDelta;

        public Vector3 ScaleHienTai => _rt != null ? _rt.localScale : Vector3.one;
        public float HoverIntensity => _hoverIntensity;
        public float TouchIntensity => _touchIntensity;

        public static NutHieuUngApple Attach(GameObject go)
        {
            if (go == null) return null;
            var old = go.GetComponent<NutHieuUng>();
            if (old != null) old.enabled = false;
            var c = go.GetComponent<NutHieuUngApple>() ?? go.AddComponent<NutHieuUngApple>();
            return c;
        }

        void Awake()
        {
            _rt = transform as RectTransform;
            _kinh = GetComponent<KinhHinh>() ?? GetComponentInChildren<KinhHinh>(true);
            LuuGoc();
        }

        void OnEnable()
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_kinh == null) _kinh = GetComponent<KinhHinh>() ?? GetComponentInChildren<KinhHinh>(true);
            LuuGoc();
            _springScale.Snap(1f);
            _springOffset.Snap(Vector2.zero);
            _springStretch.Snap(Vector2.one);
        }

        void OnDisable()
        {
            if (_daLuuGoc && _rt != null)
            {
                _rt.localScale = _scaleGoc;
                _rt.anchoredPosition = _anchorPosGoc;
            }
        }

        void LuuGoc()
        {
            if (_daLuuGoc || _rt == null) return;
            _scaleGoc = _rt.localScale;
            _anchorPosGoc = _rt.anchoredPosition;
            _daLuuGoc = true;
        }

        Vector2 LayUvCucBo(PointerEventData e)
        {
            if (_rt == null || e == null) return new Vector2(0.5f, 0.5f);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera ?? e.enterEventCamera, out Vector2 local))
                return new Vector2(0.5f, 0.5f);
            Rect r = _rt.rect;
            float u = Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1f, r.width));
            float v = Mathf.Clamp01((local.y - r.yMin) / Mathf.Max(1f, r.height));
            return new Vector2(u, v);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            _hoverUv = LayUvCucBo(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            _hovered = true;
            _hoverUv = LayUvCucBo(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            LuuGoc();
            _pressed = true;
            _touchUv = LayUvCucBo(eventData);
            _touchIntensity = 1.0f;
            _touchRadius = 42f;
            if (_rt != null)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt.parent as RectTransform, eventData.position, eventData.pressEventCamera, out _pointerDownLocal);

            // Lan nhẹ vào cha nếu có KinhHinh vùng chứa
            var parentKinh = transform.parent != null ? transform.parent.GetComponentInParent<KinhHinh>() : null;
            if (parentKinh != null && parentKinh != _kinh)
            {
                parentKinh.ChamTam = new Vector4(_touchUv.x, _touchUv.y, 140f, 0.45f);
                parentKinh.CapNhatVatLieu();
            }

            if (ReducedMotion.IsEnabled)
            {
                _springScale.Target = 1f;
            }
            else if (Application.isMobilePlatform || GiaLapIpad)
            {
                // iPad touch pop 1.15x
                _springScale.Target = 1.15f;
            }
            else
            {
                // macOS 27 nhấn nén nhẹ trước khi nảy
                _springScale.Target = 0.965f;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            _dragDelta = Vector2.zero;
            _springOffset.Target = Vector2.zero;
            _springStretch.Target = Vector2.one;

            if (ReducedMotion.IsEnabled)
            {
                _springScale.Target = 1f;
            }
            else if (!Application.isMobilePlatform && !GiaLapIpad)
            {
                // Nhả trên Mac tạo xung nảy rất khẽ (1.0 -> 1.028 -> 1.0)
                _springScale.Target = 1.0f;
                _springScale.Velocity = 0.48f;
            }
            else
            {
                _springScale.Target = 1f;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_rt == null || ReducedMotion.IsEnabled) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt.parent as RectTransform, eventData.position, eventData.pressEventCamera, out _pointerDownLocal);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_rt == null || ReducedMotion.IsEnabled) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 cur))
                return;

            _dragDelta = cur - _pointerDownLocal;
            float dx = KinhLong.CaoSu(_dragDelta.x, 26f, 0.55f);
            float dy = KinhLong.CaoSu(_dragDelta.y, 26f, 0.55f);
            _springOffset.Target = new Vector2(dx, dy);

            // Bảo toàn thể tích: giãn theo hướng kéo & co nhẹ phương vuông góc (sx * sy ≈ 1)
            float mag = Mathf.Clamp(_dragDelta.magnitude / 180f, 0f, 0.14f);
            if (mag > 0.001f)
            {
                Vector2 dir = _dragDelta.normalized;
                float stretchX = 1f + Mathf.Abs(dir.x) * mag - Mathf.Abs(dir.y) * mag * 0.5f;
                float stretchY = 1f / Mathf.Max(0.85f, stretchX);
                _springStretch.Target = new Vector2(stretchX, stretchY);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragDelta = Vector2.zero;
            _springOffset.Target = Vector2.zero;
            _springStretch.Target = Vector2.one;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _focused = true;
            if (_kinh != null) KinhLongAnhSang.QuetVien(_kinh, 0.42f);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _focused = false;
        }

        public void KichHoatKiemThuNhan(bool iPad, Vector2 dragOffset)
        {
            GiaLapIpad = iPad;
            LuuGoc();
            _pressed = true;
            _touchUv = new Vector2(0.35f, 0.65f);
            _touchIntensity = 1f;
            _touchRadius = 75f;
            if (!ReducedMotion.IsEnabled)
            {
                _springScale.Target = iPad ? 1.15f : 0.965f;
                if (dragOffset.sqrMagnitude > 0.01f)
                {
                    float dx = KinhLong.CaoSu(dragOffset.x, 26f, 0.55f);
                    float dy = KinhLong.CaoSu(dragOffset.y, 26f, 0.55f);
                    _springOffset.Target = new Vector2(dx, dy);
                    float mag = Mathf.Clamp(dragOffset.magnitude / 180f, 0f, 0.14f);
                    float sx = 1f + mag;
                    _springStretch.Target = new Vector2(sx, 1f / sx);
                }
            }
        }

        public void BuocMoPhong(float dt)
        {
            if (ReducedMotion.IsEnabled)
            {
                _springScale.Snap(1f);
                _springOffset.Snap(Vector2.zero);
                _springStretch.Snap(Vector2.one);
            }
            else
            {
                float resp = (Application.isMobilePlatform || GiaLapIpad) ? 0.28f : 0.35f;
                float damp = (Application.isMobilePlatform || GiaLapIpad) ? 0.62f : 0.55f;
                _springScale.Step(dt, resp, damp);
                _springOffset.Step(dt, 0.36f, 0.58f);
                _springStretch.Step(dt, 0.34f, 0.56f);
            }

            if (_rt != null && _daLuuGoc)
            {
                float s = _springScale.Value;
                Vector2 st = _springStretch.Value;
                _rt.localScale = new Vector3(_scaleGoc.x * s * st.x, _scaleGoc.y * s * st.y, _scaleGoc.z);
                _rt.anchoredPosition = _anchorPosGoc + _springOffset.Value;
            }

            float hoverTgt = _hovered ? 0.85f : (_focused ? 0.45f : 0f);
            _hoverIntensity = Mathf.MoveTowards(_hoverIntensity, hoverTgt, dt / 0.12f);

            if (_pressed)
                _touchRadius = Mathf.MoveTowards(_touchRadius, 140f, 260f * dt);
            else
                _touchIntensity = Mathf.MoveTowards(_touchIntensity, 0f, dt / 0.24f);

            if (_kinh != null)
            {
                _kinh.RoiTam = new Vector4(_hoverUv.x, _hoverUv.y, 95f, _hoverIntensity);
                _kinh.ChamTam = new Vector4(_touchUv.x, _touchUv.y, _touchRadius, _touchIntensity);
                if (_focused && !ReducedMotion.IsEnabled)
                    _kinh.CuongDoVienSang = Mathf.Max(_kinh.CuongDoVienSang, 1.35f);
                _kinh.CapNhatVatLieu();
            }

            if (_pressed || !_springScale.Resting() || !_springOffset.Resting() || _hoverIntensity > 0.01f || _touchIntensity > 0.01f)
                VungKinhApple.BaoBan(1);
        }

        void Update()
        {
            BuocMoPhong(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }
    }
}
