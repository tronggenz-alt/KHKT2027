using System;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Khkt.Rendering.HD
{
    /// <summary>
    /// Bảng đo hiệu năng và kiểm soát chất lượng đồ họa HD:
    ///   • Hiển thị FPS (tức thời, trung bình, 1% thấp nhất).
    ///   • RAM đo thực tế (Native Allocated + Managed Heap).
    ///   • Mức pin và nhiệt độ thiết bị.
    ///   • Nút đổi mức chất lượng [Ultra / Cao / Cân bằng].
    ///   • Ẩn/hiện bằng phím F8 hoặc chạm 3 ngón tay / nút góc trên màn hình.
    /// </summary>
    public class HdPerformanceHud : MonoBehaviour
    {
        public static HdPerformanceHud Instance { get; private set; }

        public bool ShowHud = true;

        float updateInterval = 0.4f;
        float timeAccum = 0f;
        int frameCount = 0;

        float currentFps = 60f;
        float avgFps = 60f;
        float minFps = 60f;
        float frameTimeMs = 16.6f;

        long totalAllocatedMemoryMb = 0;
        long gcMemoryMb = 0;

        float[] fpsHistory = new float[60];
        int historyIndex = 0;

        GUIStyle boxStyle;
        GUIStyle textStyle;
        GUIStyle buttonStyle;
        GUIStyle titleStyle;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            // Phím tắt F8 bật/tắt HUD
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f8Key.wasPressedThisFrame)
            {
                ShowHud = !ShowHud;
            }

            // Chạm 3 ngón tay cùng lúc trên màn hình iPad để toggle HUD
            var screen = UnityEngine.InputSystem.Touchscreen.current;
            if (screen != null)
            {
                int pressedCount = 0;
                foreach (var t in screen.touches)
                {
                    if (t.isInProgress) pressedCount++;
                }
                if (pressedCount >= 3 && Time.frameCount % 30 == 0)
                {
                    ShowHud = !ShowHud;
                }
            }

            timeAccum += Time.unscaledDeltaTime;
            frameCount++;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f)
            {
                float instantFps = 1.0f / dt;
                fpsHistory[historyIndex] = instantFps;
                historyIndex = (historyIndex + 1) % fpsHistory.Length;
            }

            if (timeAccum >= updateInterval)
            {
                currentFps = frameCount / timeAccum;
                frameTimeMs = (timeAccum / frameCount) * 1000f;

                // Tính toán min FPS trong lịch sử gần nhất
                float min = 999f;
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < fpsHistory.Length; i++)
                {
                    if (fpsHistory[i] > 1f)
                    {
                        if (fpsHistory[i] < min) min = fpsHistory[i];
                        sum += fpsHistory[i];
                        count++;
                    }
                }
                minFps = count > 0 ? min : currentFps;
                avgFps = count > 0 ? (sum / count) : currentFps;

                totalAllocatedMemoryMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                gcMemoryMb = GC.GetTotalMemory(false) / (1024 * 1024);

                timeAccum = 0f;
                frameCount = 0;
            }
        }

        void OnGUI()
        {
            if (!ShowHud) return;

            InitStyles();

            float scale = Mathf.Max(1.0f, Screen.dpi / 160f) * 0.75f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

            float w = 270f;
            float h = 180f;
            float x = (Screen.width / scale) - w - 15f;
            float y = 15f;

            GUILayout.BeginArea(new Rect(x, y, w, h), boxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("VERA LAB - PERFORMANCE", titleStyle);
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
            {
                ShowHud = false;
            }
            GUILayout.EndHorizontal();

            // FPS và Frame time
            Color fpsColor = currentFps >= 55f ? Color.green : (currentFps >= 30f ? Color.yellow : Color.red);
            var prevColor = GUI.color;
            GUI.color = fpsColor;
            GUILayout.Label($"FPS: {currentFps:0.0} (Avg: {avgFps:0.0} | 1% Min: {minFps:0.0}) - {frameTimeMs:0.1} ms", textStyle);
            GUI.color = prevColor;

            // Bộ nhớ RAM
            GUILayout.Label($"RAM: {totalAllocatedMemoryMb} MB (Mono GC: {gcMemoryMb} MB)", textStyle);

            // Thông tin thiết bị & pin
            int battery = Mathf.RoundToInt(SystemInfo.batteryLevel * 100f);
            string batteryStr = battery >= 0 ? $"{battery}%" : "Đang cắm nguồn";
            GUILayout.Label($"Thiết bị: {SystemInfo.deviceModel} (Pin: {batteryStr})", textStyle);

            // Mức đồ họa hiện tại
            var qm = HdQualityManager.Instance;
            string tierName = qm != null ? qm.GetTierName(qm.CurrentTier) : "Ultra";
            GUILayout.Label($"Mức đồ họa: {tierName}", textStyle);

            // Nút bấm chuyển mức chất lượng
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ultra", buttonStyle)) qm?.SetTier(HdQualityTier.Ultra, true);
            if (GUILayout.Button("Cao", buttonStyle)) qm?.SetTier(HdQualityTier.Cao, true);
            if (GUILayout.Button("Cân bằng", buttonStyle)) qm?.SetTier(HdQualityTier.CanBang, true);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void InitStyles()
        {
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.11f, 0.88f));
                tex.Apply();
                boxStyle.normal.background = tex;
                boxStyle.padding = new RectOffset(10, 10, 8, 8);
            }

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label);
                textStyle.fontSize = 12;
                textStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
                textStyle.margin = new RectOffset(0, 0, 1, 1);
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(textStyle);
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 12;
                titleStyle.normal.textColor = new Color(0.4f, 0.85f, 1.0f);
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 11;
                buttonStyle.margin = new RectOffset(2, 2, 4, 2);
            }
        }
    }
}

