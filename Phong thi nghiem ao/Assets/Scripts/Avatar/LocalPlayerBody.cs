using System.Collections.Generic;
using Khkt.Environment;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khkt.Avatar
{
    /// <summary>
    /// Thân người của chính người chơi ở góc nhìn thứ nhất: khi cúi xuống nhìn thấy áo blouse trắng,
    /// quần và giày đứng trên sàn. Đầu/kính ẩn để không che camera; có thể bật/tắt trong Cài đặt.
    /// </summary>
    public class LocalPlayerBody : MonoBehaviour
    {
        public const string BodyVisiblePrefKey = "KHKT_HIEN_THAN_MINH";

        static bool loaded;
        static bool visible = true;

        public static LocalPlayerBody Instance { get; private set; }

        public static bool Visible
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    visible = PlayerPrefs.GetInt(BodyVisiblePrefKey, 1) != 0;
                }
                return visible;
            }
            set
            {
                loaded = true;
                visible = value;
                PlayerPrefs.SetInt(BodyVisiblePrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (Instance != null) Instance.ApplyVisibility();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            visible = true;
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoAttach()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryAttachInActiveScene();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttachInActiveScene();

        public static void TryAttachInActiveScene()
        {
            var viewer = FindAnyObjectByType<DesktopViewer>();
            if (viewer == null) return;
            if (viewer.GetComponent<LocalPlayerBody>() == null)
                viewer.gameObject.AddComponent<LocalPlayerBody>();
        }

        DesktopViewer viewer;
        GameObject bodyRoot;
        readonly List<Renderer> renderers = new List<Renderer>();

        void Awake()
        {
            Instance = this;
            viewer = GetComponent<DesktopViewer>();
            BuildLowerBody();
            ApplyVisibility();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (bodyRoot != null) Destroy(bodyRoot);
        }

        void BuildLowerBody()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var coat = new Material(lit) { hideFlags = HideFlags.DontSave };
            coat.SetColor("_BaseColor", new Color(0.95f, 0.96f, 0.98f));
            coat.SetFloat("_Smoothness", 0.14f);

            var trim = new Material(lit) { hideFlags = HideFlags.DontSave };
            trim.SetColor("_BaseColor", new Color(0.28f, 0.62f, 0.90f));
            trim.SetFloat("_Smoothness", 0.22f);

            var pants = new Material(lit) { hideFlags = HideFlags.DontSave };
            pants.SetColor("_BaseColor", new Color(0.15f, 0.18f, 0.24f));
            pants.SetFloat("_Smoothness", 0.18f);

            var shoes = new Material(lit) { hideFlags = HideFlags.DontSave };
            shoes.SetColor("_BaseColor", new Color(0.10f, 0.11f, 0.14f));
            shoes.SetFloat("_Smoothness", 0.28f);

            bodyRoot = new GameObject("Thân mình (Góc nhìn thứ nhất)");

            // Ngực và vạt áo blouse dưới tầm camera
            AddPart(PrimitiveType.Capsule, bodyRoot.transform, "Ngực áo blouse",
                new Vector3(0f, 1.12f, 0f), new Vector3(0.32f, 0.24f, 0.19f), Vector3.zero, coat);
            AddPart(PrimitiveType.Cylinder, bodyRoot.transform, "Vạt áo blouse",
                new Vector3(0f, 0.84f, 0f), new Vector3(0.33f, 0.15f, 0.20f), Vector3.zero, coat);
            AddPart(PrimitiveType.Cube, bodyRoot.transform, "Túi áo",
                new Vector3(-0.08f, 1.18f, 0.095f), new Vector3(0.068f, 0.075f, 0.012f), Vector3.zero, trim);

            float[] legX = { -0.092f, 0.092f };
            for (int i = 0; i < 2; i++)
            {
                AddPart(PrimitiveType.Capsule, bodyRoot.transform, $"Chân {(i == 0 ? "trái" : "phải")}",
                    new Vector3(legX[i], 0.42f, 0f), new Vector3(0.12f, 0.38f, 0.12f), Vector3.zero, pants);
                AddPart(PrimitiveType.Capsule, bodyRoot.transform, $"Giày {(i == 0 ? "trái" : "phải")}",
                    new Vector3(legX[i], 0.042f, 0.045f), new Vector3(0.10f, 0.10f, 0.065f), new Vector3(90f, 0f, 0f), shoes);
            }
        }

        void AddPart(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers.Add(r);
        }

        void ApplyVisibility()
        {
            bool on = Visible;
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].enabled = on;
        }

        void LateUpdate()
        {
            if (bodyRoot == null || viewer == null) return;
            float yaw = viewer.transform.eulerAngles.y;
            Quaternion flatRot = Quaternion.Euler(0f, yaw, 0f);
            // Đẩy nhẹ ra sau ~10 cm so với trục mắt để cúi nhìn xuống thấy rõ ngực áo, chân giày mà không cắt mặt gần camera
            Vector3 anchor = viewer.transform.position - flatRot * new Vector3(0f, 0f, 0.10f);
            bodyRoot.transform.SetPositionAndRotation(anchor, flatRot);
        }
    }
}
