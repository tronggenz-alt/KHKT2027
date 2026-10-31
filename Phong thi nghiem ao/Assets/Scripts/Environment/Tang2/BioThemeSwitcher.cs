using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Đổi chủ đề tầng 2 (Quốc tế ↔ Tối giản), phím F7. Chỉ đổi vật ở tầng 2 (y ≥ 3,6 m), không sửa asset vật liệu.
    /// Hoạt cảnh 0,6 s: mỗi vật liệu đang dùng được thay tạm bằng một bản sao, màu / độ bóng / phát sáng nội suy theo đường cong
    /// emphasized decelerate, xong thì gán hẳn vật liệu đích (giữ gộp lô). Khu được tải lại sau đó cũng nhận đúng chủ đề.
    /// </summary>
    public class BioThemeSwitcher : MonoBehaviour
    {
        public static BioThemeSwitcher Instance { get; private set; }

        public BioThemeMap themeMap;
        public LabThemeMode currentTheme = LabThemeMode.QuocTe;
        public float transitionDuration = 0.6f;
        public bool isTransitioning;

        const string PrefKey = "KHKT_CHU_DE";
        const float Floor2Y = 3.72f;  // tâm vật cao hơn mặt trần tầng 1 (3,6..3,7) mới là vật tầng 2

        void Awake()
        {
            Instance = this;
            string env = System.Environment.GetEnvironmentVariable(PrefKey);
            if (!string.IsNullOrEmpty(env))
                currentTheme = env.ToLowerInvariant().Contains("toi_gian") ? LabThemeMode.ToiGian : LabThemeMode.QuocTe;
            else if (PlayerPrefs.HasKey(PrefKey) && System.Enum.TryParse(PlayerPrefs.GetString(PrefKey), out LabThemeMode saved))
                currentTheme = saved;
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void Start()
        {
            if (currentTheme != LabThemeMode.QuocTe) ApplyInstant(currentTheme);
        }

        void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (currentTheme == LabThemeMode.QuocTe || isTransitioning) return;
            foreach (var root in s.GetRootGameObjects()) Apply(root.GetComponentsInChildren<Renderer>(true), currentTheme);
            foreach (var root in s.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<ThemeOnly>(true)) t.ApplyTheme(currentTheme);
        }

        void Update()
        {
            var kb = Keyboard.current;  // phím tắt giao diện (được phép đọc trực tiếp)
            if (kb != null && kb.f7Key.wasPressedThisFrame) ToggleTheme();
        }

        public void ToggleTheme() => SetTheme(currentTheme == LabThemeMode.QuocTe ? LabThemeMode.ToiGian : LabThemeMode.QuocTe);

        public void SetTheme(LabThemeMode target)
        {
            if (isTransitioning || target == currentTheme || themeMap == null) return;
            StartCoroutine(Transition(target));
        }

        static Renderer[] Floor2Renderers()
        {
            var all = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            var list = new List<Renderer>(all.Length / 2);
            foreach (var r in all)
                if (r != null && r.bounds.center.y >= Floor2Y && !(r is ParticleSystemRenderer)) list.Add(r);
            return list.ToArray();
        }

        void Apply(IEnumerable<Renderer> renderers, LabThemeMode theme)
        {
            foreach (var r in renderers)
            {
                if (r == null || r.bounds.center.y < Floor2Y) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = themeMap.GetMaterial(mats[i], theme);
                    if (m != null) { mats[i] = m; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        public void ApplyInstant(LabThemeMode theme)
        {
            currentTheme = theme;
            Apply(Floor2Renderers(), theme);
            foreach (var t in FindObjectsByType<ThemeOnly>(FindObjectsInactive.Include)) t.ApplyTheme(theme);
        }

        IEnumerator Transition(LabThemeMode target)
        {
            isTransitioning = true;
            var renderers = Floor2Renderers();
            // vật liệu tạm cho từng vật liệu nguồn đang dùng
            var temps = new Dictionary<Material, (Material temp, Material to)>();
            var slots = new List<(Renderer r, Material[] mats)>();
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool any = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var from = mats[i];
                    var to = themeMap.GetMaterial(from, target);
                    if (to == null) continue;
                    if (!temps.TryGetValue(from, out var e))
                    {
                        e = (new Material(from) { name = from.name + " (đổi chủ đề)" }, to);
                        temps[from] = e;
                    }
                    mats[i] = e.temp;
                    any = true;
                }
                if (any) { r.sharedMaterials = mats; slots.Add((r, mats)); }
            }
            var onlys = FindObjectsByType<ThemeOnly>(FindObjectsInactive.Include);
            foreach (var t in onlys) if (t.theme == target) { t.gameObject.SetActive(true); t.transform.localScale = new Vector3(1, 0.001f, 1); }

            float el = 0;
            while (el < transitionDuration)
            {
                el += Time.unscaledDeltaTime;
                float k = Easing.EmphasizedDecelerate(Mathf.Clamp01(el / transitionDuration));
                foreach (var kv in temps)
                {
                    var from = kv.Key;
                    var (temp, to) = kv.Value;
                    temp.SetColor("_BaseColor", Color.Lerp(from.GetColor("_BaseColor"), to.GetColor("_BaseColor"), k));
                    temp.SetFloat("_Smoothness", Mathf.Lerp(from.GetFloat("_Smoothness"), to.GetFloat("_Smoothness"), k));
                    if (temp.IsKeywordEnabled("_EMISSION") || to.IsKeywordEnabled("_EMISSION"))
                    {
                        temp.EnableKeyword("_EMISSION");
                        temp.SetColor("_EmissionColor", Color.Lerp(from.GetColor("_EmissionColor"), to.GetColor("_EmissionColor"), k));
                    }
                }
                foreach (var t in onlys)
                    t.transform.localScale = new Vector3(1, t.theme == target ? Mathf.Max(0.001f, k) : Mathf.Max(0.001f, 1 - k), 1);
                yield return null;
            }

            // gán hẳn vật liệu đích, bỏ vật liệu tạm
            foreach (var (r, mats) in slots)
            {
                if (r == null) continue;
                var cur = r.sharedMaterials;
                for (int i = 0; i < cur.Length; i++)
                    foreach (var kv in temps)
                        if (cur[i] == kv.Value.temp) cur[i] = kv.Value.to;
                r.sharedMaterials = cur;
            }
            foreach (var kv in temps) Destroy(kv.Value.temp);
            foreach (var t in onlys)
            {
                t.transform.localScale = Vector3.one;
                t.ApplyTheme(target);
            }
            currentTheme = target;
            PlayerPrefs.SetString(PrefKey, currentTheme.ToString());
            PlayerPrefs.Save();
            isTransitioning = false;
        }
    }
}
