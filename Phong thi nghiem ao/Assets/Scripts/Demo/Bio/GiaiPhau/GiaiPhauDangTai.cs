using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo.Bio.GiaiPhau
{
    /// <summary>
    /// Màn hình "Đang tải mô hình giải phẫu 3D…" phủ kín màn hình, bật TRƯỚC khi dựng phòng giải phẫu để khoảng khựng của khung
    /// hình vẽ đầu tiên (mô hình 250 nghìn đỉnh lên GPU, biên dịch shader: đo được 660 ms lần đầu) nằm sau màn hình tải
    /// thay vì làm lab đứng hình. Lớp phủ riêng (Screen Space Overlay), không phụ thuộc camera nào.
    /// </summary>
    public sealed class GiaiPhauDangTai : MonoBehaviour
    {
        static GiaiPhauDangTai instance;
        GameObject root;
        Text dots;
        float t0;

        public static bool DangHien => instance != null && instance.root != null && instance.root.activeSelf;

        public static void Hien()
        {
            if (instance == null)
            {
                var go = new GameObject("GiaiPhau_DangTai");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GiaiPhauDangTai>();
                instance.Dung();
            }
            instance.root.SetActive(true);
            instance.t0 = Time.unscaledTime;
        }

        public static void An()
        {
            if (instance != null && instance.root != null) instance.root.SetActive(false);
        }

        void Dung()
        {
            root = new GameObject("Lớp phủ", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var cv = root.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 32000;
            var sc = root.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;

            var bg = new GameObject("Nền", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var r = (RectTransform)bg.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 1f);
            bg.GetComponent<Image>().raycastTarget = true;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text Chu(string ten, string s, int cỡ, Color m, float y)
            {
                var g = new GameObject(ten, typeof(RectTransform), typeof(Text));
                g.transform.SetParent(root.transform, false);
                var rt = (RectTransform)g.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1400, 90);
                rt.anchoredPosition = new Vector2(0, y);
                var t = g.GetComponent<Text>();
                t.font = GlassKit.Regular != null ? GlassKit.Regular : font;
                t.text = s; t.fontSize = cỡ; t.color = m; t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                return t;
            }
            Chu("Tiêu đề", "Đang tải mô hình giải phẫu 3D", 52, new Color(0.92f, 0.95f, 1f, 1f), 40);
            dots = Chu("Điểm", "", 40, new Color(0.2f, 0.84f, 1f, 1f), -30);
            Chu("Ghi chú", "Xương, cơ, da và các cơ quan thật (BodyParts3D). Lần đầu có thể mất vài giây.", 26, new Color(0.62f, 0.7f, 0.82f, 1f), -110);
            root.SetActive(false);
        }

        void Update()
        {
            if (root == null || !root.activeSelf || dots == null) return;
            int n = 1 + (int)((Time.unscaledTime - t0) * 3f) % 3;
            dots.text = new string('.', n * 2 - 1).Replace(".", ". ").TrimEnd();
        }

        /// <summary>Hiện màn hình tải, chờ 2 khung hình cho nó được vẽ thật, rồi chạy việc nặng.</summary>
        public static IEnumerator ChayDuoiManHinhTai(System.Action viecNang, float giuThem = 0.25f)
        {
            Hien();
            yield return null;
            yield return null;
            viecNang?.Invoke();
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(giuThem);
            An();
        }
    }
}
