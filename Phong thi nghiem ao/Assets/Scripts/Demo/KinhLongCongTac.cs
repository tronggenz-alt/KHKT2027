using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Chuyển động lỏng cho công tắc (Toggle):
    ///   - Khi nghỉ: núm tròn/tròn nhẹ đặc trắng, không phình thừa.
    ///   - Khi nhấn/giữ ngoài Phòng Lab: núm biến hình mượt thành thấu kính KinhHinh (tiết diện Lip 1.55x × 1.30x)
    ///     phóng đại rãnh màu phía dưới như WWDC25 4f; khi thả thu lại lò xo (0.38, 0.62).
    ///   - Trong Phòng Lab hoặc tắt Kính Lỏng: giữ nguyên cơ chế sprite NumKinh cũ.
    /// </summary>
    public class KinhLongCongTac : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Toggle toggle;
        public RectTransform knob;
        public Image knobImage;
        public float travel = 12f;
        public Vector2 knobSize = new Vector2(22f, 22f);

        LoXo posX;
        LoXo glass = new LoXo(0f);
        Image glassOverlay;
        KinhHinh _kinhThauKinh;
        bool down;
        bool init;
        bool _epKinhApple;

        public bool DangDungThauKinhApple => _kinhThauKinh != null;

        public static KinhLongCongTac Attach(Toggle t, RectTransform knob, Image knobImage, float travel)
        {
            if (t == null || knob == null) return null;
            var c = t.GetComponent<KinhLongCongTac>() ?? t.gameObject.AddComponent<KinhLongCongTac>();
            c.toggle = t;
            c.knob = knob;
            c.knobImage = knobImage;
            c.travel = travel;
            c.knobSize = knob.sizeDelta;
            c.EnsureGlass();
            c.Snap();
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

        void EnsureGlass()
        {
            if (knob == null) return;
            if (glassOverlay == null)
            {
                var t = knob.Find("KinhNum");
                GameObject go;
                if (t != null)
                {
                    go = t.gameObject;
                }
                else
                {
                    go = new GameObject("KinhNum", typeof(RectTransform), typeof(Image));
                    go.layer = knob.gameObject.layer;
                    go.transform.SetParent(knob, false);
                }
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                glassOverlay = go.GetComponent<Image>();
                glassOverlay.raycastTarget = false;
            }

            if (NenDungKinhApple())
            {
                glassOverlay.sprite = null;
                _kinhThauKinh = glassOverlay.GetComponent<KinhHinh>() ?? glassOverlay.gameObject.AddComponent<KinhHinh>();
                _kinhThauKinh.CheDo = KinhLongCheDo.Clear;
                _kinhThauKinh.LoaiMatCat = KinhLongMatCat.Lip;
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

        void Snap()
        {
            if (knob == null || toggle == null) return;
            float tx = toggle.isOn ? travel : -travel;
            posX = new LoXo(tx);
            glass.Snap(0f);
            init = true;
            Apply();
        }

        void OnEnable()
        {
            if (init) Snap();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (toggle != null && !toggle.interactable) return;
            down = true;
        }

        public void OnPointerUp(PointerEventData e) => down = false;
        public void OnPointerExit(PointerEventData e) => down = false;

        public void DatTrangThaiKiemThu(bool dangNhan, float giaTriGlass)
        {
            if (!init) Snap();
            down = dangNhan;
            glass.Snap(Mathf.Clamp01(giaTriGlass));
            Apply();
        }

        void LateUpdate()
        {
            if (!init) Snap();
            if (knob == null || toggle == null) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            posX.Target = toggle.isOn ? travel : -travel;
            glass.Target = (down && GlassKit.LiquidGlass) ? 1f : 0f;

            if (ReducedMotion.IsEnabled)
            {
                posX.Snap(posX.Target);
                glass.Snap(0f);
            }
            else
            {
                posX.Step(dt, 0.34f, 0.68f);
                glass.Step(dt, glass.Target > 0.5f ? 0.16f : 0.38f, glass.Target > 0.5f ? 0.85f : 0.62f);
            }
            Apply();
        }

        void Apply()
        {
            if (knob == null) return;
            float g = Mathf.Clamp01(glass.Value);
            float v = Mathf.Abs(posX.Velocity);
            float stretch = ReducedMotion.IsEnabled ? 0f : Mathf.Clamp(v * 0.00055f, 0f, 0.24f);

            bool apple = NenDungKinhApple();
            float wMul = apple ? (1f + 0.55f * g + stretch) : (1f + 0.35f * g + stretch);
            float hMul = apple ? (1f + 0.30f * g - stretch * 0.25f) : (1f + 0.12f * g - stretch * 0.3f);

            knob.anchoredPosition = new Vector2(posX.Value, 0f);
            knob.sizeDelta = new Vector2(knobSize.x * wMul, knobSize.y * hMul);

            if (knobImage != null)
            {
                var c = knobImage.color;
                c.a = Mathf.Lerp(1f, apple ? 0.04f : 0.14f, g);
                knobImage.color = c;
            }

            if (glassOverlay != null)
            {
                if (_kinhThauKinh != null && apple)
                {
                    glassOverlay.enabled = g > 0.02f;
                    var gc = glassOverlay.color;
                    gc.a = g;
                    glassOverlay.color = gc;
                    _kinhThauKinh.CuongDoKhucXa = Mathf.Lerp(0.4f, 1.65f, g);
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
    }
}
