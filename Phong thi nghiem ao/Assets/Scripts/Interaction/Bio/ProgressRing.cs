using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Vòng tiến trình hiển thị ở tâm màn hình khi người chơi giữ chuột trái để thực hiện
    /// thao tác sinh học có thời lượng (ví dụ: hơ đỏ que cấy 2,0 s, hạ lamen góc 45° 1,0 s, thấm thuốc nhuộm 0,8 s).
    /// </summary>
    public class ProgressRing : MonoBehaviour
    {
        public static ProgressRing Instance { get; private set; }

        float currentProgress01;
        string currentActionLabel = "";
        bool isVisible;

        Texture2D barBgTex, barFillTex;
        GUIStyle labelStyle, pctStyle;

        public static ProgressRing Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ProgressRing>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            var go = new GameObject("BioProgressRing");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ProgressRing>();
            return Instance;
        }

        public static void Show(string actionLabel, float progress01)
        {
            var inst = Ensure();
            inst.currentActionLabel = actionLabel ?? "Đang thao tác...";
            inst.currentProgress01 = Mathf.Clamp01(progress01);
            inst.isVisible = true;
        }

        public static void Hide()
        {
            if (Instance != null)
                Instance.isVisible = false;
        }

        void EnsureStyles()
        {
            if (barBgTex != null) return;
            barBgTex = MakeTex(new Color(0.08f, 0.12f, 0.14f, 0.85f));
            barFillTex = MakeTex(new Color(0.22f, 0.88f, 0.52f, 0.96f));
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            pctStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.98f, 0.84f) }
            };
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(2, 2);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (!isVisible) return;
            EnsureStyles();

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f + 46f;
            float w = 220f, h = 10f;

            GUI.DrawTexture(new Rect(cx - w * 0.5f - 2f, cy - 2f, w + 4f, h + 4f), barBgTex);
            GUI.DrawTexture(new Rect(cx - w * 0.5f, cy, w * currentProgress01, h), barFillTex);
            GUI.Label(new Rect(cx - 160f, cy - 24f, 320f, 22f), currentActionLabel, labelStyle);
            GUI.Label(new Rect(cx - 80f, cy + 12f, 160f, 20f), $"{Mathf.RoundToInt(currentProgress01 * 100f)} %", pctStyle);
        }
    }
}
