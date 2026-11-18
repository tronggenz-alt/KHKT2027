using Khkt.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Khkt.Avatar
{
    /// <summary>
    /// Chèn các công tắc G2 (Găng tay nitrile xanh, Hiện thân người góc nhìn thứ nhất)
    /// vào "Tab Điều khiển" trong IntroScreen lúc chạy mà không sửa IntroScreen.cs.
    /// </summary>
    public class AvatarSettingsHook : MonoBehaviour
    {
        static AvatarSettingsHook instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureHook();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => EnsureHook();

        public static void EnsureHook()
        {
            if (FindAnyObjectByType<IntroScreen>() == null) return;
            if (instance != null) return;
            var go = new GameObject("AvatarSettingsHook");
            instance = go.AddComponent<AvatarSettingsHook>();
        }

        Toggle gloveToggle;
        Toggle bodyToggle;

        void Update()
        {
            if (gloveToggle == null || bodyToggle == null)
                TryInject();
            else
                SyncValues();
        }

        void TryInject()
        {
            var intro = FindAnyObjectByType<IntroScreen>();
            if (intro == null) return;

            Transform searchRoot = intro.ui != null && intro.ui.Canvas != null
                ? intro.ui.Canvas.transform
                : intro.transform;

            Transform tabControls = FindChildRecursive(searchRoot, "Tab Điều khiển");
            if (tabControls == null)
            {
                var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
                for (int i = 0; i < allCanvases.Length; i++)
                {
                    tabControls = FindChildRecursive(allCanvases[i].transform, "Tab Điều khiển");
                    if (tabControls != null) break;
                }
            }
            if (tabControls == null) return;

            var existingGlove = tabControls.Find("ToggleDeoGangTay");
            if (existingGlove != null)
            {
                gloveToggle = existingGlove.GetComponent<Toggle>();
            }
            else
            {
                gloveToggle = GlassKit.MakeToggle(tabControls, "ToggleDeoGangTay",
                    "Đeo găng tay nitrile xanh (an toàn phòng lab)",
                    new Vector2(0f, -312f), new Vector2(694f, 46f),
                    HandRig.WearGloves, v => HandRig.WearGloves = v);
            }

            var existingBody = tabControls.Find("ToggleHienThanMinh");
            if (existingBody != null)
            {
                bodyToggle = existingBody.GetComponent<Toggle>();
            }
            else
            {
                bodyToggle = GlassKit.MakeToggle(tabControls, "ToggleHienThanMinh",
                    "Hiện thân người góc nhìn thứ nhất",
                    new Vector2(0f, -366f), new Vector2(694f, 46f),
                    LocalPlayerBody.Visible, v => LocalPlayerBody.Visible = v);
            }
        }

        void SyncValues()
        {
            if (gloveToggle != null) gloveToggle.SetIsOnWithoutNotify(HandRig.WearGloves);
            if (bodyToggle != null) bodyToggle.SetIsOnWithoutNotify(LocalPlayerBody.Visible);
        }

        static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.name == targetName) return c;
                Transform found = FindChildRecursive(c, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
