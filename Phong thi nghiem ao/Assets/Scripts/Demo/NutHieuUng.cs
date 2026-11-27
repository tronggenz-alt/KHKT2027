using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Khkt.Environment.Tang2;
using Khkt.Interaction.Bio;

namespace Khkt.Demo
{
    /// <summary>
    /// Phản hồi chạm kiểu Liquid Glass (glassEffect .interactive() của Apple), viết lại 27/11/2026:
    ///   • Nhấn: nút phồng lên (~10 px tổng, tối đa 8 %) bằng lò xo nhanh; kính sáng lên từ ĐÚNG điểm chạm (KinhLongAnhCham).
    ///   • Giữ và kéo: nút co giãn đàn hồi theo hướng kéo (cao su), quầng sáng đi theo ngón tay / con trỏ.
    ///   • Nhả: bật về có nảy nhẹ như giọt nước; quầng sáng bừng rồi lan rộng và tắt dần.
    ///   • Mac/Windows: rê chuột qua nút có quầng sáng dịu đi theo con trỏ (không phồng).
    ///   • Co giãn quanh tâm nút, không lệch theo pivot; không đè bố cục (LayoutGroup).
    ///   • Luật an toàn giữ nguyên: nút giữ luôn nhả khi nhấc ngón, trượt ra ngoài, SetActive(false), mất focus.
    ///   • Giảm chuyển động: không phồng, không co giãn, không nảy; chỉ còn quầng sáng mờ dần.
    /// </summary>
    public class NutHieuUng : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler, IPointerMoveHandler
    {
        public const string SoundPrefKey = "KHKT_AM_THANH_GIAO_DIEN";

        public static bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SoundPrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SoundPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [Header("Cấu hình hiệu ứng")]
        [Tooltip("Rê chuột (Mac/Windows): nút nhích to nhẹ.")]
        public float hoverScale = 1.022f;
        [Tooltip("Giao diện Cổ điển: nút co lại khi nhấn. Kính lỏng thì phồng lên.")]
        public float pressedScale = 0.94f;
        public bool isHoldButton = false;
        public Action onHoldReleaseAction;
        [Tooltip("Phồng khi nhấn (true, kiểu Liquid Glass) hay co lại (false, kiểu cũ).")]
        public bool liquid = true;

        RectTransform rectTransform;
        Image legacyOverlay;
        KinhLongAnhCham anh;
        Image hinh;
        Selectable sel;

        Vector3 baseScale = Vector3.one;
        Vector3 lastOffset;
        bool isPointerDown;
        bool hovering;
        bool dangChay;

        LoXo phong = new LoXo(1f);        // hệ số phồng
        LoXo2 gian = new LoXo2(Vector2.zero);   // co giãn theo hướng kéo (tỉ lệ)
        LoXo2 doi = new LoXo2(Vector2.zero);    // độ dời theo hướng kéo (px)
        LoXo sang = new LoXo(0f);         // độ sáng quầng
        LoXo lan = new LoXo(1f);          // bán kính quầng (bội số)
        Vector2 tamSang, tamDich;          // tâm quầng (cục bộ, gốc ở tâm)
        Vector2 diemNhan;                  // điểm bắt đầu nhấn (cục bộ)
        float bungNha;                     // năng lượng lúc nhả, tắt dần

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null) baseScale = rectTransform.localScale;
            sel = GetComponent<Selectable>();
            hinh = GetComponent<Image>();
            if (hinh == null && sel != null) hinh = sel.targetGraphic as Image;
            liquid = liquid && GlassKit.LiquidGlass;
            var t = transform.Find("HighlightOverlay");
            if (t != null)
            {
                legacyOverlay = t.GetComponent<Image>();
                if (legacyOverlay != null) legacyOverlay.color = GlassKit.Wash(0f);
            }
        }

        bool CoTheBam => sel == null || sel.IsInteractable();

        void EnsureAnh()
        {
            if (anh != null) return;
            var t = transform.Find("Ánh chạm");
            if (t != null) anh = t.GetComponent<KinhLongAnhCham>();
            if (anh == null)
            {
                var go = new GameObject("Ánh chạm", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var le = go.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                anh = go.AddComponent<KinhLongAnhCham>();
            }
            // nằm ngay trên nền nút, dưới chữ và biểu tượng
            int idx = 0;
            var vien = transform.Find("Viền");
            if (vien != null) idx = vien.GetSiblingIndex() + 1;
            anh.transform.SetSiblingIndex(Mathf.Min(idx, transform.childCount - 1));
            anh.raycastTarget = false;
            SyncShape();
        }

        void SyncShape()
        {
            if (anh == null) return;
            if (hinh != null && hinh.sprite == GlassKit.Rounded)
            {
                // mặt kính lỏng: sprite mang dữ liệu khúc xạ, mượn hình đặc cùng bán kính (góc 30) cho quầng sáng
                anh.shape = GlassKit.Solid;
                anh.multiplier = hinh.pixelsPerUnitMultiplier;
            }
            else if (hinh != null && hinh.sprite != null && hinh.type == Image.Type.Sliced)
            {
                anh.shape = hinh.sprite;
                anh.multiplier = hinh.pixelsPerUnitMultiplier;
            }
            else if (hinh != null && hinh.sprite != null)
            {
                anh.shape = hinh.sprite;
                anh.multiplier = 1f;
            }
            else
            {
                anh.shape = GlassKit.Solid;
                anh.multiplier = 3f;
            }
            anh.color = MauAnh();
        }

        /// <summary>Nút nền sáng (chủ đề Trắng, nút phụ) dùng quầng màu nhấn dịu; nút nền tối hoặc màu nhấn dùng quầng trắng.</summary>
        Color MauAnh()
        {
            Color fill = hinh != null ? hinh.color : Color.black;
            float lum = 0.2126f * fill.r + 0.7152f * fill.g + 0.0722f * fill.b;
            if (lum > 0.78f && fill.a > 0.5f)
                return new Color(GlassKit.Accent.r, GlassKit.Accent.g, GlassKit.Accent.b, 0.30f);
            return new Color(1f, 1f, 1f, UiTheme.IsLight ? 0.42f : 0.30f);
        }

        Vector2 Local(PointerEventData e)
        {
            if (rectTransform == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera ?? e.enterEventCamera, out var p);
            return p - rectTransform.rect.center;
        }

        static bool LaCamUng(PointerEventData e) =>
            e is ExtendedPointerEventData x && (x.pointerType == UIPointerType.Touch || x.pointerType == UIPointerType.Tracked);

        float Pop()
        {
            if (!liquid) return Mathf.Clamp(pressedScale, 0.8f, 1f);
            if (rectTransform == null) return 1.05f;
            Vector2 s = rectTransform.rect.size;
            float big = Mathf.Max(Mathf.Max(s.x, s.y), 1f);
            return 1f + Mathf.Clamp(10f / big, 0.02f, 0.08f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CoTheBam) return;
            isPointerDown = true;
            EnsureAnh();
            diemNhan = Local(eventData);
            tamDich = diemNhan;
            if (sang.Value < 0.05f) tamSang = diemNhan;
            phong.Target = ReducedMotion.IsEnabled ? 1f : Pop();
            sang.Target = 1f;
            lan.Target = 1f;
            dangChay = true;
            if (SoundEnabled)
            {
                if (isHoldButton) BioFeedback.PlayButton();
                else BioFeedback.PlayClick();
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!isPointerDown && !hovering) return;
            Vector2 p = Local(eventData);
            tamDich = p;
            if (isPointerDown && !ReducedMotion.IsEnabled && rectTransform != null)
            {
                Vector2 size = rectTransform.rect.size;
                Vector2 d = p - diemNhan;
                // dời đàn hồi tối đa ~6 px, giãn theo trục kéo tối đa ~6 %, trục kia co lại nửa chừng (giữ thể tích)
                doi.Target = new Vector2(KinhLong.CaoSu(d.x, 12f), KinhLong.CaoSu(d.y, 12f)) * 0.5f;
                float gx = KinhLong.CaoSu(Mathf.Abs(d.x), Mathf.Max(size.x, 1f) * 0.12f) / Mathf.Max(size.x, 1f);
                float gy = KinhLong.CaoSu(Mathf.Abs(d.y), Mathf.Max(size.y, 1f) * 0.12f) / Mathf.Max(size.y, 1f);
                gian.Target = new Vector2(gx - gy * 0.5f, gy - gx * 0.5f);
            }
            dangChay = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isPointerDown)
            {
                isPointerDown = false;
                Nha(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            if (isPointerDown)
            {
                isPointerDown = false;
                Nha(false);
                ReleaseHold();
            }
            else
            {
                sang.Target = 0f;
                phong.Target = 1f;
                dangChay = true;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // chỉ chuột mới có trạng thái rê; cảm ứng không có
            if (LaCamUng(eventData) || isPointerDown || !CoTheBam) return;
            hovering = true;
            EnsureAnh();
            tamDich = Local(eventData);
            if (sang.Value < 0.05f) tamSang = tamDich;
            sang.Target = 0.32f;
            lan.Target = 1.15f;
            if (!ReducedMotion.IsEnabled) phong.Target = hoverScale;
            dangChay = true;
        }

        void Nha(bool bamTrung)
        {
            phong.Target = hovering && !ReducedMotion.IsEnabled ? hoverScale : 1f;
            gian.Target = Vector2.zero;
            doi.Target = Vector2.zero;
            if (bamTrung && !ReducedMotion.IsEnabled)
            {
                // bừng sáng rồi lan rộng, tắt dần
                bungNha = 1f;
                sang.Value = Mathf.Max(sang.Value, 1.15f);
                lan.Target = 1.9f;
            }
            sang.Target = hovering ? 0.32f : 0f;
            dangChay = true;
        }

        void LateUpdate()
        {
            if (!dangChay) return;
            float dt = Time.unscaledDeltaTime;
            bool reduced = ReducedMotion.IsEnabled;

            bool nhan = isPointerDown;
            phong.Step(dt, nhan ? KinhLong.NhanResponse : KinhLong.NhaResponse, nhan ? KinhLong.NhanDamping : KinhLong.NhaDamping);
            gian.Step(dt, nhan ? 0.18f : KinhLong.NhaResponse, nhan ? 0.9f : KinhLong.NhaDamping);
            doi.Step(dt, nhan ? 0.18f : KinhLong.NhaResponse, nhan ? 0.9f : KinhLong.NhaDamping);
            sang.Step(dt, sang.Target > sang.Value ? 0.14f : (bungNha > 0f ? 0.55f : 0.32f), 1f);
            lan.Step(dt, 0.5f, 1f);
            tamSang = Vector2.Lerp(tamSang, tamDich, 1f - Mathf.Exp(-dt * 22f));
            bungNha = Mathf.Max(0f, bungNha - dt * 2.2f);
            if (bungNha <= 0f && !nhan && !hovering) lan.Target = 1f;

            if (rectTransform != null)
            {
                if (reduced)
                {
                    KinhLong.ApplyAroundCenter(rectTransform, baseScale, Vector2.one, Vector2.zero, ref lastOffset);
                }
                else
                {
                    float p = phong.Value;
                    Vector2 g = gian.Value;
                    KinhLong.ApplyAroundCenter(rectTransform, baseScale, new Vector2(p * (1f + g.x), p * (1f + g.y)), doi.Value, ref lastOffset);
                }
            }

            if (anh != null)
            {
                Vector2 size = rectTransform != null ? rectTransform.rect.size : new Vector2(100, 44);
                float r = Mathf.Max(size.y * 1.1f, Mathf.Min(size.x, size.y * 3f) * 0.55f) * lan.Value;
                float s = Mathf.Max(0f, sang.Value);
                // lớp sáng đều nhẹ khi nhấn (cả nút sáng lên một chút), quầng tập trung ở điểm chạm
                anh.Set(tamSang, r, liquid ? s * 0.85f : 0f, Mathf.Clamp01(s) * (isPointerDown ? (liquid ? 0.16f : 0.22f) : 0.06f));
            }

            bool nghi = !isPointerDown && !hovering && phong.Resting(0.0004f) && gian.Resting(0.0004f) && doi.Resting(0.02f)
                        && Mathf.Abs(sang.Value - sang.Target) < 0.003f && bungNha <= 0f && lan.Resting(0.002f);
            if (nghi)
            {
                phong.Snap(1f); gian.Snap(Vector2.zero); doi.Snap(Vector2.zero);
                if (rectTransform != null) KinhLong.ApplyAroundCenter(rectTransform, baseScale, Vector2.one, Vector2.zero, ref lastOffset);
                if (anh != null && sang.Target <= 0f) { sang.Snap(0f); anh.Set(tamSang, 1f, 0f, 0f); }
                dangChay = hovering;  // rê chuột: tiếp tục cho quầng đi theo con trỏ
            }
        }

        void OnDisable()
        {
            ResetImmediate();
            ReleaseHold();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetImmediate();
                ReleaseHold();
            }
        }

        void ReleaseHold()
        {
            if (isHoldButton && onHoldReleaseAction != null)
            {
                try { onHoldReleaseAction.Invoke(); }
                catch (Exception ex) { Debug.LogWarning($"[NutHieuUng] Lỗi nhả nút giữ: {ex.Message}"); }
            }
        }

        void ResetImmediate()
        {
            isPointerDown = false;
            hovering = false;
            bungNha = 0f;
            phong.Snap(1f); gian.Snap(Vector2.zero); doi.Snap(Vector2.zero);
            sang.Snap(0f); lan.Snap(1f);
            if (rectTransform != null) KinhLong.ApplyAroundCenter(rectTransform, baseScale, Vector2.one, Vector2.zero, ref lastOffset);
            if (anh != null) anh.Set(tamSang, 1f, 0f, 0f);
            if (legacyOverlay != null) legacyOverlay.color = GlassKit.Wash(0f);
            dangChay = false;
        }

        /// <summary>
        /// Gắn nhanh hiệu ứng NutHieuUng vào bất kỳ GameObject nút bấm nào.
        /// </summary>
        public static NutHieuUng Attach(GameObject go, bool isHold = false, Action onRelease = null)
        {
            if (go == null) return null;
            var eff = go.GetComponent<NutHieuUng>();
            if (eff == null) eff = go.AddComponent<NutHieuUng>();
            eff.isHoldButton = isHold;
            if (onRelease != null) eff.onHoldReleaseAction = onRelease;
            return eff;
        }
    }
}
