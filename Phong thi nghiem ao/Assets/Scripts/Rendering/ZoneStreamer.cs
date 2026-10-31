using System.Collections;
using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Khkt.Rendering
{
    /// <summary>
    /// Tải màn chơi cộng dồn (additive scene loading) + lưu trạng thái.
    ///
    /// Mỗi khu (ZoneMap) có một cảnh riêng chứa phần tĩnh: tủ, kệ, bàn, trang trí, đèn. Chỉ các khu đang cần mới được tải:
    ///   - khu người xem đang đứng (ra hiên thì cả phòng lab và phòng hóa chất, vì nhìn qua cửa sổ thấy được);
    ///   - khu hai bên mọi cửa trong vòng preloadRadius quanh người xem (tải trước, cửa chỉ mở khi khu bên kia đã sẵn sàng);
    ///   - khu robot đang đứng và các khu trên đường robot sắp đi;
    ///   - khu đang có việc chưa xong (máy phân tích đang chạy...), khu được yêu cầu (dịch chuyển tới trạm).
    /// Khu không cần nữa quá unloadDelayS giây thì gỡ hẳn khỏi bộ nhớ.
    ///
    /// Trạng thái: đồ tương tác của mỗi khu (lọ, dụng cụ, máy, chỗ đặt...) nằm ở cảnh lõi dưới một ZoneRoot. Gỡ khu thì nhánh
    /// đó "ngủ" (tắt hẳn: không vẽ, không chạy), tải lại thì thức dậy y nguyên: vị trí, dung dịch, nhiệt độ, máy đang bật.
    /// Đồ đã mang sang khu khác (hoặc đang cầm) được chuyển sang gốc của khu mới trước khi khu cũ ngủ.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ZoneStreamer : MonoBehaviour
    {
        public static ZoneStreamer Instance { get; private set; }

        public Transform viewer;
        [Tooltip("Tới gần cửa trong khoảng này thì tải trước khu bên kia (m)")]
        public float preloadRadius = 4.5f;
        [Tooltip("Khu không còn cần bao lâu thì gỡ (s)")]
        public float unloadDelayS = 2.5f;
        [Tooltip("Tắt: tải mọi khu và giữ nguyên (để so sánh hiệu năng)")]
        public bool streaming = true;
        [Tooltip("Ưu tiên tải nền. BelowNormal: dựng cảnh rải ra nhiều khung hình, đỡ giật khi đi qua cửa")]
        public ThreadPriority loadPriority = ThreadPriority.BelowNormal;

        enum State { Unloaded, Loading, Loaded, Unloading }

        static int N => ZoneMap.Zones.Length;
        readonly State[] state = new State[ZoneMap.Zones.Length];
        readonly float[] lastNeeded = new float[ZoneMap.Zones.Length];
        readonly float[] requestedAt = new float[ZoneMap.Zones.Length];
        readonly float[] loadStarted = new float[ZoneMap.Zones.Length];
        readonly bool[] need = new bool[ZoneMap.Zones.Length];
        readonly Dictionary<object, (float until, Vector3[] points)> keeps = new Dictionary<object, (float, Vector3[])>();
        readonly List<object> expired = new List<object>();
        bool started;
        Transform strayRoot;
        CanvasGroup overlay;
        Text overlayText;

        /// <summary>Khu vừa tải xong (true) hoặc vừa gỡ (false).</summary>
        public static event System.Action<int, bool> ZoneChanged;

        public int Loads { get; private set; }
        public int Unloads { get; private set; }
        public float LastLoadMs { get; private set; }

        /// <summary>Không có khu nào đang tải / gỡ và mọi khu cần đều đã sẵn sàng (kiểm thử chờ cái này sau khi dịch chuyển).</summary>
        public static bool Settled => Instance == null || Instance.IsSettled();

        public bool IsLoaded(int zone) => zone >= 0 && zone < N && state[zone] == State.Loaded;

        /// <summary>Khu đã sẵn sàng (Outside luôn sẵn sàng: hiên, sân nằm ở cảnh lõi).</summary>
        public bool IsReady(int zone) => zone < 0 || zone >= N || state[zone] == State.Loaded;

        public static bool ReadyAt(Vector3 p) => Instance == null || Instance.IsReady(ZoneMap.At(p));

        // ------------------------------------------------------------------

        void Awake()
        {
            Instance = this;
            foreach (var r in FindObjectsByType<ZoneRoot>(FindObjectsInactive.Include)) r.Register();
            for (int z = 0; z < N; z++)
            {
                state[z] = SceneManager.GetSceneByName(ZoneMap.Zones[z].scene).isLoaded ? State.Loaded : State.Unloaded;
                requestedAt[z] = -99f;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // Khu cần ngay lúc đầu: tải đồng bộ (xong trước khung hình đầu tiên được vẽ)
            ComputeNeed();
            for (int z = 0; z < N; z++)
                if ((need[z] || !streaming) && state[z] == State.Unloaded)
                {
                    state[z] = State.Loading;
                    loadStarted[z] = Time.realtimeSinceStartup;
                    SceneManager.LoadScene(ZoneMap.Zones[z].scene, LoadSceneMode.Additive);
                }
        }

        IEnumerator Start()
        {
            // Để mọi đồ tương tác khởi tạo đủ (Awake / Start) rồi mới cho các khu không cần ngủ
            yield return null;
            yield return null;
            started = true;
            ComputeNeed();
            for (int z = 0; z < N; z++)
            {
                if (need[z] || !streaming) { lastNeeded[z] = Time.time; continue; }
                Park(z);
                if (state[z] == State.Loaded) Unload(z);
            }
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!started) return;
            ComputeNeed();
            float now = Time.time;
            for (int z = 0; z < N; z++)
            {
                if (need[z] || !streaming)
                {
                    lastNeeded[z] = now;
                    if (state[z] == State.Unloaded) Load(z);
                }
                else if (state[z] == State.Loaded && now - lastNeeded[z] > unloadDelayS)
                {
                    Unload(z);
                }
            }
            UpdateOverlay();
        }

        void ComputeNeed()
        {
            for (int z = 0; z < N; z++) need[z] = false;
            if (viewer != null)
            {
                Vector3 v = viewer.position;
                int vz = ZoneMap.At(v);
                if (vz >= 0) need[vz] = true;
                else if (v.y < 3.6f) { need[ZoneMap.Lab] = true; need[ZoneMap.Store] = true; }  // ngoài hiên tầng 1: nhìn qua cửa sổ vào trong
                foreach (var d in AutoDoor.All)
                {
                    Vector3 dv = d.Center - v;
                    if (Mathf.Abs(dv.y) > 2.5f) continue;  // không nạp trước cửa ở tầng khác
                    if (dv.x * dv.x + dv.z * dv.z > preloadRadius * preloadRadius) continue;
                    Mark(d.zoneA);
                    Mark(d.zoneB);
                }
            }
            var bot = LabRobot.Instance;
            if (bot != null) Mark(ZoneMap.At(bot.transform.position));
            float now = Time.time;
            expired.Clear();
            foreach (var kv in keeps)
            {
                if (kv.Value.until < now) { expired.Add(kv.Key); continue; }
                foreach (var p in kv.Value.points) Mark(ZoneMap.At(p));
            }
            foreach (var k in expired) keeps.Remove(k);
            for (int z = 0; z < N; z++)
                if (now - requestedAt[z] < unloadDelayS) need[z] = true;
        }

        void Mark(int z)
        {
            if (z >= 0 && z < N) need[z] = true;
        }

        bool IsSettled()
        {
            if (!started) return false;
            for (int z = 0; z < N; z++)
            {
                if (state[z] == State.Loading || state[z] == State.Unloading) return false;
                if ((need[z] || !streaming) && state[z] != State.Loaded) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Yêu cầu từ bên ngoài
        // ------------------------------------------------------------------

        /// <summary>Cần khu này ngay (cửa có người tới gần, dịch chuyển tới trạm...). Giữ trong unloadDelayS giây kể từ lần gọi cuối.</summary>
        public void Request(int zone)
        {
            if (zone < 0 || zone >= N) return;
            requestedAt[zone] = Time.time;
            if (started && state[zone] == State.Unloaded) Load(zone);
        }

        /// <summary>Giữ các khu chứa những điểm này trong ít nhất seconds giây (robot đang đi, máy đang chạy). Gọi lại để gia hạn.</summary>
        public void Keep(object owner, float seconds, params Vector3[] points)
        {
            if (owner == null || points == null) return;
            keeps[owner] = (Time.time + seconds, points);
            foreach (var p in points) Request(ZoneMap.At(p));
        }

        public void KeepPath(object owner, List<Vector3> path, float seconds = 30f)
        {
            if (path != null) Keep(owner, seconds, path.ToArray());
        }

        public void Release(object owner) => keeps.Remove(owner);

        /// <summary>Chờ tới khi khu chứa p sẵn sàng (tối đa timeout giây).</summary>
        public static IEnumerator WaitReadyAt(Vector3 p, float timeout = 10f)
        {
            var zs = Instance;
            if (zs == null) yield break;
            int z = ZoneMap.At(p);
            float t0 = Time.realtimeSinceStartup;
            while (!zs.IsReady(z) && Time.realtimeSinceStartup - t0 < timeout)
            {
                zs.Request(z);
                yield return null;
            }
        }

        // ------------------------------------------------------------------
        // Tải / gỡ
        // ------------------------------------------------------------------

        void Load(int z)
        {
            state[z] = State.Loading;
            loadStarted[z] = Time.realtimeSinceStartup;
            Application.backgroundLoadingPriority = loadPriority;
            var op = SceneManager.LoadSceneAsync(ZoneMap.Zones[z].scene, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogWarning($"[KHKT-KHU] Không tải được cảnh {ZoneMap.Zones[z].scene} (chưa có trong Build Settings?)");
                state[z] = State.Unloaded;
                requestedAt[z] = -99f;
            }
        }

        void Unload(int z)
        {
            Park(z);
            var scene = SceneManager.GetSceneByName(ZoneMap.Zones[z].scene);
            if (!scene.isLoaded) { state[z] = State.Unloaded; return; }
            state[z] = State.Unloading;
            SceneManager.UnloadSceneAsync(scene);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            int z = IndexOf(scene.name);
            if (z < 0) return;
            state[z] = State.Loaded;
            LastLoadMs = (Time.realtimeSinceStartup - loadStarted[z]) * 1000f;
            Loads++;
            if (started) Unpark(z);
            Debug.Log($"[KHKT-KHU] Đã tải {ZoneMap.Zones[z].name} ({LastLoadMs:0} ms)");
            ZoneChanged?.Invoke(z, true);
        }

        void OnSceneUnloaded(Scene scene)
        {
            int z = IndexOf(scene.name);
            if (z < 0) return;
            state[z] = State.Unloaded;
            Unloads++;
            Debug.Log($"[KHKT-KHU] Đã gỡ {ZoneMap.Zones[z].name}");
            ZoneChanged?.Invoke(z, false);
            // Đang gỡ mà lại cần (vừa quay lại cửa): Update sẽ tải lại ở khung hình sau
        }

        static int IndexOf(string sceneName)
        {
            for (int z = 0; z < N; z++) if (ZoneMap.Zones[z].scene == sceneName) return z;
            return -1;
        }

        /// <summary>
        /// Cho đồ tương tác của khu ngủ. Trước đó chuyển những vật đã ra khỏi khu (mang sang khu khác, đang cầm trên tay,
        /// robot đang mang) sang gốc của khu chứa chúng hiện giờ, để chúng không bị tắt theo.
        /// </summary>
        void Park(int z)
        {
            var root = ZoneRoot.Of(z);
            if (root == null || root.Parked) return;
            foreach (var g in root.GetComponentsInChildren<Grabbable>(true))
            {
                var up = g.transform.parent != null ? g.transform.parent.GetComponentInParent<Grabbable>(true) : null;
                if (up != null && up.transform.IsChildOf(root.transform)) continue;  // vật con đi theo vật cha
                int at = ZoneMap.At(g.transform.position);
                if (at == z && !g.IsHeld) continue;
                Transform to = at >= 0 && at != z && ZoneRoot.Of(at) != null ? ZoneRoot.Of(at).transform : StrayRoot();
                g.transform.SetParent(to, true);
            }
            root.gameObject.SetActive(false);
        }

        void Unpark(int z)
        {
            var root = ZoneRoot.Of(z);
            if (root != null && root.Parked) root.gameObject.SetActive(true);
        }

        /// <summary>
        /// Đánh thức một vật đang ngủ (ở khu đã gỡ) khi nó được cầm lên từ xa (tra cứu "lấy hộ", kiểm thử): chuyển sang gốc khu
        /// chứa điểm near (thường là chỗ người cầm đang đứng). Vật thường đã thức thì không làm gì.
        /// </summary>
        public static void Wake(Transform t, Vector3 near)
        {
            if (t == null || t.gameObject.activeInHierarchy) return;
            var root = ZoneRoot.Of(ZoneMap.At(near));
            if (root != null && !root.Parked) t.SetParent(root.transform, true);
            else if (Instance != null) t.SetParent(Instance.StrayRoot(), true);
        }

        /// <summary>Chỗ chứa đồ đang ở ngoài mọi khu (mang ra hiên, đang cầm khi đứng ở cửa): luôn thức.</summary>
        Transform StrayRoot()
        {
            if (strayRoot == null)
            {
                strayRoot = new GameObject("Đồ ngoài các khu").transform;
                strayRoot.SetParent(transform.root, false);
            }
            return strayRoot;
        }

        // ------------------------------------------------------------------
        // Màn mờ khi dịch chuyển tới khu chưa tải
        // ------------------------------------------------------------------

        void UpdateOverlay()
        {
            bool waiting = viewer != null && !IsReady(ZoneMap.At(viewer.position));
            if (overlay == null)
            {
                if (!waiting) return;
                BuildOverlay();
            }
            float target = waiting ? 1f : 0f;
            overlay.alpha = Mathf.MoveTowards(overlay.alpha, target, Time.unscaledDeltaTime * (waiting ? 12f : 4f));
            if (waiting) overlayText.text = "Đang tải " + ZoneMap.Name(ZoneMap.At(viewer.position)).ToLowerInvariant() + "…";
            bool show = overlay.alpha > 0.001f;
            if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        }

        void BuildOverlay()
        {
            var go = new GameObject("Màn mờ tải khu", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            overlay = go.GetComponent<CanvasGroup>();
            overlay.alpha = 0;
            overlay.blocksRaycasts = false;
            var bg = new GameObject("Nền", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var rt = (RectTransform)bg.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 1f);
            var txt = new GameObject("Chữ", typeof(RectTransform), typeof(Text));
            txt.transform.SetParent(go.transform, false);
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(900, 80);
            overlayText = txt.GetComponent<Text>();
            overlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            overlayText.fontSize = 30;
            overlayText.alignment = TextAnchor.MiddleCenter;
            overlayText.color = new Color(0.85f, 0.92f, 1f);
        }
    }
}
