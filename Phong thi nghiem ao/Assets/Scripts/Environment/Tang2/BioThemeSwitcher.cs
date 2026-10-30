using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Environment.Tang2
{
    public class BioThemeSwitcher : MonoBehaviour
    {
        public static BioThemeSwitcher Instance { get; private set; }

        [Header("Cấu hình chủ đề")]
        public BioThemeMap themeMap;
        public LabThemeMode currentTheme = LabThemeMode.QuocTe;
        public float transitionDuration = 0.6f;

        [Header("Trạng thái")]
        public bool isTransitioning;

        const string PrefKey = "KHKT_CHU_DE";

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            // Đọc cấu hình từ biến môi trường hoặc PlayerPrefs
            string env = System.Environment.GetEnvironmentVariable(PrefKey);
            if (!string.IsNullOrEmpty(env))
            {
                if (env.ToLower().Contains("toi_gian") || env.ToLower().Contains("minimal"))
                    currentTheme = LabThemeMode.ToiGian;
                else
                    currentTheme = LabThemeMode.QuocTe;
            }
            else if (PlayerPrefs.HasKey(PrefKey))
            {
                string saved = PlayerPrefs.GetString(PrefKey);
                if (System.Enum.TryParse(saved, out LabThemeMode mode))
                    currentTheme = mode;
            }
        }

        void Start()
        {
            ApplyThemeInstant(currentTheme);
        }

        void Update()
        {
            // Phím F7 để chuyển đổi chủ đề
            var kb = Keyboard.current;
            if (kb != null && kb.f7Key.wasPressedThisFrame)
            {
                ToggleTheme();
            }
        }

        public void ToggleTheme()
        {
            if (isTransitioning) return;
            var targetTheme = (currentTheme == LabThemeMode.QuocTe) ? LabThemeMode.ToiGian : LabThemeMode.QuocTe;
            StartCoroutine(TransitionRoutine(targetTheme));
        }

        public void SetTheme(LabThemeMode newTheme)
        {
            if (isTransitioning || currentTheme == newTheme) return;
            StartCoroutine(TransitionRoutine(newTheme));
        }

        IEnumerator TransitionRoutine(LabThemeMode targetTheme)
        {
            isTransitioning = true;
            float elapsed = 0f;

            // Thu thập các ThemeOnly component
            var themeOnlys = Object.FindObjectsByType<ThemeOnly>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                // Đường cong emphasized decelerate (0.05, 0.7, 0.1, 1)
                float curve = Easing.EmphasizedDecelerate(t);

                yield return null;
            }

            ApplyThemeInstant(targetTheme);
            currentTheme = targetTheme;
            PlayerPrefs.SetString(PrefKey, currentTheme.ToString());
            PlayerPrefs.Save();

            isTransitioning = false;
        }

        public void ApplyThemeInstant(LabThemeMode theme)
        {
            currentTheme = theme;

            // Cập nhật ThemeOnly components
            var themeOnlys = Object.FindObjectsByType<ThemeOnly>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var to in themeOnlys)
            {
                if (to != null) to.ApplyTheme(theme);
            }

            if (themeMap == null) return;

            // Tìm tất cả Renderers ở Tầng 2 (vị trí y >= 3.6m hoặc là con của 'Tầng 2')
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (r == null || r.transform.position.y < 3.6f) continue;

                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var mapped = themeMap.GetMaterial(mats[i], theme);
                    if (mapped != null && mapped != mats[i])
                    {
                        mats[i] = mapped;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
