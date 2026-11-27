using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Chuyển động lỏng cho núm thanh trượt (Slider):
    ///   - Khi nghỉ: giữ nguyên hình tròn trắng sạch và đúng chuẩn hình vuông (|w - h| &lt; 0.5f cho KhktUiRunner).
    ///   - Khi kéo ngoài Phòng Lab: phóng nở mượt thành thấu kính Squircle (1.70x × 1.30x) có KinhHinh
    ///     khúc xạ thanh ray phía dưới (~1.18x zoom), tắt đi khi dừng yên >0.18s và co về khi nhả tay.
    ///   - Trong Phòng Lab: giữ nguyên hành vi NumKinh cũ.
    /// </summary>
    public class KinhLongNumTruot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Slider slider;
        public RectTransform handle;
        public Image ring;
        public Image core;

        LoXo glass = new LoXo(0f);
        Image glassOverlay;
        KinhHinh _kinhThauKinh;
        Vector2 restSize;
        bool down;
        bool sizeRecorded;
        bool _epKinhApple;
        float prevVal;
        float idleSince;
        bool daNghi;

        public static KinhLongNumTruot Attach(Slider s, RectTransform handle, Image ring, Image core)
        {
            if (s == null || handle == null) return null;
            var c = s.GetComponent<KinhLongNumTruot>() ?? s.gameObject.AddComponent<KinhLongNumTruot>();
            c.slider = s;
            c.handle = handle;
            c.ring = ring;
            c.core = core;
            c.restSize = handle.sizeDelta;
            c.sizeRecorded = c.restSize.sqrMagnitude > 1f;
            c.prevVal = s.value;
            return c;
        }

        public void BatCheDoApple(bool ep = true)
        {
            _epKinhApple = ep;
            EnsureGlass();
        }

        bool NenDungKinhApple()
        {
            if (!GlassKit.LiquidGlass) return false;
            if (_epKinhApple) return true;
            return GetComponentInParent<HaiTangCanvas>() != null || GetComponentInParent<VungKinhApple>() != null;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (slider != null && !slider.interactable) return;
            down = true;
            daNghi = false;
            idleSince = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData e) { down = false; daNghi = false; }

        void OnDisable()
        {
            down = false;
            daNghi = false;
            glass.Snap(0f);
            if (sizeRecorded) Apply(0f, 0f);
        }

        public void DatTrangThaiKiemThu(bool dangNhan, float giaTriGlass)
        {
            if (!sizeRecorded && handle != null)
            {
                restSize = handle.sizeDelta;
                sizeRecorded = restSize.sqrMagnitude > 1f;
            }
            down = dangNhan;
            daNghi = false;
            idleSince = Time.unscaledTime + 2f;
            glass.Snap(Mathf.Clamp01(giaTriGlass));
            Apply(glass.Value, 0f);
        }

        void LateUpdate()
        {
            if (slider == null || handle == null) return;
            if (!sizeRecorded && handle.rect.width > 1f)
            {
                restSize = handle.sizeDelta;
                sizeRecorded = true;
            }
            if (!sizeRecorded) return;

            float cur = slider.value;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float speed = Mathf.Abs(cur - prevVal) / Mathf.Max(dt, 0.001f);
            prevVal = cur;

            if (down)
            {
                if (speed > 0.02f) { idleSince = Time.unscaledTime; daNghi = false; }
                else if (Time.unscaledTime - idleSince > 0.18f) daNghi = true;
            }

            glass.Target = (down && !daNghi && GlassKit.LiquidGlass) ? 1f : 0f;
            if (ReducedMotion.IsEnabled) glass.Snap(0f);
            else glass.Step(dt, glass.Target > 0.5f ? 0.18f : 0.36f, glass.Target > 0.5f ? 0.88f : 0.62f);

            float stretch = ReducedMotion.IsEnabled ? 0f : Mathf.Clamp(speed * 0.05f, 0f, 0.20f);
            Apply(glass.Value, stretch);
        }

        void Apply(float g, float stretch)
        {
            float baseW = restSize.x;
            float baseH = restSize.y;
            float w = baseW * (1f + 0.70f * g + stretch);
            float h = baseH * (1f + 0.30f * g - stretch * 0.25f);
            handle.sizeDelta = new Vector2(w, h);

            if (ring != null)
            {
                var c = ring.color;
                c.a = Mathf.Lerp(1f, 0.06f, g);
                ring.color = c;
            }
            if (core != null)
            {
                var c = core.color;
                c.a = Mathf.Lerp(1f, 0.04f, g);
                core.color = c;
            }

            if (g > 0.01f) EnsureGlass();
            if (glassOverlay != null)
            {
                if (_kinhThauKinh != null && NenDungKinhApple())
                {
                    glassOverlay.enabled = g > 0.01f;
                    var gc = glassOverlay.color;
                    gc.a = g;
                    glassOverlay.color = gc;
                    // Độ tụ thấu kính ~1.18x phóng đại thanh trượt phía dưới (WWDC25 4g)
                    _kinhThauKinh.CuongDoKhucXa = Mathf.Lerp(0.5f, 1.85f, g);
                    _kinhThauKinh.CapNhatVatLieu();
                }
                else
                {
                    glassOverlay.enabled = true;
                    var gc = glassOverlay.color;
                    gc.a = g;
                    glassOverlay.color = gc;
                }
            }

            if (g > 0.01f) VungKinhApple.BaoBan(1);
        }

        void EnsureGlass()
        {
            if (glassOverlay != null) return;
            var go = new GameObject("KinhNum", typeof(RectTransform), typeof(Image));
            go.layer = handle.gameObject.layer;
            go.transform.SetParent(handle, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            glassOverlay = go.GetComponent<Image>();
            glassOverlay.raycastTarget = false;

            if (NenDungKinhApple())
            {
                glassOverlay.sprite = null;
                _kinhThauKinh = go.AddComponent<KinhHinh>();
                _kinhThauKinh.CheDo = KinhLongCheDo.Clear;
                _kinhThauKinh.LoaiMatCat = KinhLongMatCat.Squircle;
                _kinhThauKinh.HinhVienNang = true;
                _kinhThauKinh.LaNenSang = true;
                _kinhThauKinh.LaDieuHuong = GetComponentInParent<HaiTangCanvas>() != null;
                _kinhThauKinh.MucMo = 0.05f;
                _kinhThauKinh.CuongDoKhucXa = 2.0f;
                _kinhThauKinh.VienKhucXa = 22f;
                _kinhThauKinh.CuongDoNen = 0.04f;
                _kinhThauKinh.CuongDoVienSang = 1.45f;
                _kinhThauKinh.CuongDoVienToi = 0.22f;
                _kinhThauKinh.BongCuongDo = 0.22f;
                _kinhThauKinh.CapNhatVatLieu();
            }
            else
            {
                glassOverlay.sprite = KinhLong.NumKinh;
                glassOverlay.color = new Color(1f, 1f, 1f, 0f);
            }
        }
    }
}
