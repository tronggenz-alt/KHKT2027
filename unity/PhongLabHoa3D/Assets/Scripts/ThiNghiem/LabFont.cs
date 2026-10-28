// Font có đủ tiếng Việt (DejaVu Sans, giấy phép tự do – Resources/PTN/Font/LICENSE_DEJAVU.txt).
// Font dựng sẵn "LegacyRuntime" của Unity trên WebGL thiếu chữ có dấu (Đ, ề, ả…) nên HUD OnGUI và chữ 3D hiện "Đi u khi n".
// LabFont.Get() trả font này (rơi về LegacyRuntime nếu thiếu); mọi OnGUI dùng chung GUI.skin nên chỉ cần đặt GUI.skin.font một lần.
using UnityEngine;

namespace PhongThiNghiem
{
    public static class LabFont
    {
        static Font _font;

        public static Font Get()
        {
            if (_font == null) _font = Resources.Load<Font>("PTN/Font/DejaVuSans");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void TuGan()
        {
            var g = new GameObject("LabFontOnGUI") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(g);
            g.AddComponent<LabFontOnGUI>();
        }
    }

    [DefaultExecutionOrder(-10000)]
    sealed class LabFontOnGUI : MonoBehaviour
    {
        void OnGUI()
        {
            var f = LabFont.Get();
            if (f != null && GUI.skin.font != f) GUI.skin.font = f;
        }
    }
}
