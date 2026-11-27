using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Giọt chọn kính lỏng cho một nhóm chip (tương ứng GlassEffectContainer + glassEffectID của Apple):
    /// khi chỉ một chip trong nhóm đang bật, nền "đã chọn" là MỘT giọt kính dùng chung; đổi chip thì giọt trượt sang
    /// bằng lò xo, kéo dài theo hướng chạy và thắt lại như giọt nước, sáng lên khi đang bay rồi lắng xuống.
    /// Nhóm bật nhiều chip cùng lúc thì giọt tan đi và từng chip tự tô nền như cũ. Không ảnh hưởng LayoutGroup.
    /// Gắn tự động từ GlassKit.SetChip (thành phần nằm trên cha của các chip).
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongGiotChon : MonoBehaviour
    {
        readonly HashSet<Button> dangBat = new HashSet<Button>();
        RectTransform giot;
        Image nen, vien, anh;
        Button dich;
        Button anTren;       // chip đang được giọt thay nền (nền chip ẩn đi)
        LoXo2 tam, co;
        LoXo hien = new LoXo(0f);
        bool coViTri;

        public static void Bao(Button chip, bool on)
        {
            if (chip == null || chip.transform.parent == null) return;
            var p = chip.transform.parent;
            var g = p.GetComponent<KinhLongGiotChon>();
            if (g == null)
            {
                if (!on) return;
                g = p.gameObject.AddComponent<KinhLongGiotChon>();
            }
            if (on) g.dangBat.Add(chip); else g.dangBat.Remove(chip);
            g.enabled = true;
        }

        void Ensure()
        {
            if (giot != null) return;
            var go = new GameObject("Giọt chọn", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            giot = (RectTransform)go.transform;
            giot.anchorMin = giot.anchorMax = new Vector2(0.5f, 0.5f);
            giot.pivot = new Vector2(0.5f, 0.5f);
            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            nen = go.AddComponent<Image>();
            nen.sprite = GlassKit.Solid;
            nen.type = Image.Type.Sliced;
            nen.raycastTarget = false;

            vien = NewChild("Viền giọt", GlassKit.Border);
            anh = NewChild("Ánh giọt", KinhLong.NumKinh);
            giot.SetAsFirstSibling();
        }

        Image NewChild(string name, Sprite sp)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(giot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var im = go.GetComponent<Image>();
            im.sprite = sp;
            im.type = Image.Type.Sliced;
            im.raycastTarget = false;
            return im;
        }

        bool RectOf(Button b, out Vector2 center, out Vector2 size)
        {
            center = size = Vector2.zero;
            if (b == null || !b.gameObject.activeInHierarchy) return false;
            var rt = (RectTransform)b.transform;
            var p = (RectTransform)transform;
            Vector3 w = rt.TransformPoint(rt.rect.center);
            Vector3 l = p.InverseTransformPoint(w);
            center = new Vector2(l.x, l.y);
            size = Vector2.Scale(rt.rect.size, new Vector2(rt.localScale.x, rt.localScale.y));
            return true;
        }

        void Restore()
        {
            if (anTren == null) return;
            SetChipBg(anTren, 1f);
            anTren = null;
        }

        static void SetChipBg(Button b, float a)
        {
            var img = b.targetGraphic as Image;
            if (img != null) img.canvasRenderer.SetAlpha(a);
            var v = b.transform.Find("Viền");
            var vi = v != null ? v.GetComponent<Image>() : null;
            if (vi != null) vi.canvasRenderer.SetAlpha(a);
        }

        void LateUpdate()
        {
            dangBat.RemoveWhere(b => b == null);
            Button one = null;
            int count = 0;
            foreach (var b in dangBat) if (b.gameObject.activeInHierarchy) { one = b; count++; }
            if (count != 1) one = null;

            float dt = Time.unscaledDeltaTime;
            bool reduced = ReducedMotion.IsEnabled;

            if (one != null && RectOf(one, out var c, out var s))
            {
                Ensure();
                if (!coViTri || reduced || hien.Value < 0.02f) { tam.Snap(c); co.Snap(s); coViTri = true; }
                tam.Target = c;
                co.Target = s;
                if (dich != one)
                {
                    Restore();
                    dich = one;
                }
                hien.Target = 1f;
            }
            else
            {
                hien.Target = 0f;
                if (count > 1) Restore();
            }

            if (giot == null) return;
            hien.Step(dt, 0.25f, 1f);
            Vector2 pos = tam.Step(dt, KinhLong.TruotResponse, KinhLong.TruotDamping);
            Vector2 sz = co.Step(dt, KinhLong.TruotResponse, KinhLong.TruotDamping);
            Vector2 v = tam.Velocity;

            // kéo dài theo hướng bay, thắt lại chiều kia (giữ diện tích) — giọt nước đang chảy
            float sp = v.magnitude;
            float st = reduced ? 0f : Mathf.Clamp(sp / 2200f, 0f, 0.32f);
            Vector2 dir = sp > 1f ? v / sp : Vector2.right;
            Vector2 stretched = new Vector2(
                sz.x * (1f + st * Mathf.Abs(dir.x)) * (1f - st * 0.35f * Mathf.Abs(dir.y)),
                sz.y * (1f + st * Mathf.Abs(dir.y)) * (1f - st * 0.35f * Mathf.Abs(dir.x)));
            giot.localPosition = new Vector3(pos.x, pos.y, 0f);
            giot.sizeDelta = stretched;
            if (giot.GetSiblingIndex() != 0) giot.SetAsFirstSibling();

            float a = Mathf.Clamp01(hien.Value);
            float bay = reduced ? 0f : Mathf.Clamp01(sp / 900f);   // đang bay: kính trong và sáng hơn
            Color fill = GlassKit.AccentSoft;
            float m = UiTheme.IsVera ? 2.2f : 3f;
            if (dich != null && dich.targetGraphic is Image di) m = di.pixelsPerUnitMultiplier;
            nen.pixelsPerUnitMultiplier = m;
            vien.pixelsPerUnitMultiplier = m;
            nen.color = new Color(fill.r, fill.g, fill.b, Mathf.Max(fill.a, 0.9f) * a * (1f - 0.45f * bay));
            var ac = GlassKit.Accent;
            vien.color = new Color(ac.r, ac.g, ac.b, (UiTheme.IsLight ? 0.50f : 0.65f) * a);
            anh.pixelsPerUnitMultiplier = KinhLong.NangMultiplier(Mathf.Max(stretched.y, 1f)) * 0.75f;
            anh.color = new Color(1f, 1f, 1f, a * (0.18f + 0.55f * bay));

            // chip đích ẩn nền riêng khi giọt đã tới nơi (giọt làm nền thay)
            if (dich != null && one == dich)
            {
                float arrive = Mathf.Clamp01(1f - (tam.Target - pos).magnitude / Mathf.Max(sz.x * 0.5f, 1f));
                SetChipBg(dich, 1f - arrive * a);
                anTren = dich;
            }

            if (hien.Target <= 0f && hien.Value < 0.01f)
            {
                Restore();
                dich = null;
                coViTri = false;
                nen.color = Color.clear; vien.color = Color.clear; anh.color = Color.clear;
                if (dangBat.Count == 0) enabled = false;
            }
        }

        void OnDisable()
        {
            if (anTren != null) { SetChipBg(anTren, 1f); anTren = null; }
        }
    }
}
