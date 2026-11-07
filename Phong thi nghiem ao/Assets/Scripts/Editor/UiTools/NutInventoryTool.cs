using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Khkt.Demo;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Công cụ Kiểm kê Nút bấm uGUI và Touch Triggers trên toàn dự án VERA Lab (Giai đoạn 0).
    ///   • Quét mọi GameObject có Button, TouchTapTrigger, TouchHoldTrigger.
    ///   • Đo đạc kích thước theo Canvas Units và tính quy đổi sang Points trên màn hình iPad Air M3 (2360x1640)
    ///     và iPad Pro 12.9" (2732x2048).
    ///   • Phát hiện phím tắt cứng máy tính (Q, F, Space, Enter, C, X, Z, Esc, WASD...) trong nội dung nhãn.
    ///   • Xuất dữ liệu kiểm kê ra docs/giao-dien-hd/nut_goc.json.
    /// </summary>
    public static class NutInventoryTool
    {
        public const string OutputPath = "docs/giao-dien-hd/nut_goc.json";

        [System.Serializable]
        public class NutRecord
        {
            public string scene;
            public string path;
            public string name;
            public string triggerType;
            public float posX;
            public float posY;
            public float width;
            public float height;
            public float ptWidth2360;
            public float ptHeight2360;
            public float ptWidth2732;
            public float ptHeight2732;
            public bool reaches44ptAirM3;
            public bool reaches44ptPro129;
            public string label;
            public string shortcutKey;
            public bool hasVisualSublayer;
            public bool hasSoundFeedback;
        }

        [System.Serializable]
        public class NutInventoryData
        {
            public string timestamp;
            public string gitCommit;
            public int totalButtons;
            public int buttonsUnder44pt;
            public int buttonsWithKeyboardShortcuts;
            public List<NutRecord> buttons = new List<NutRecord>();
        }

        [MenuItem("KHKT/UI/Kiểm kê nút")]
        public static void InventoryMenu()
        {
            RunInventory(false);
        }

        public static void ExportBatch()
        {
            RunInventory(true, "docs/giao-dien-hd/nut_goc.json", false);
        }

        public static void ExportUpdatedBatch()
        {
            RunInventory(true, "docs/giao-dien-hd/nut_cap_nhat.json", true);
        }

        public static void RunInventory(bool isBatch, string targetRelPath = OutputPath, bool touchMode = false)
        {
            Debug.Log($"[UI-INVENTORY] Bắt đầu kiểm kê toàn diện nút bấm giao diện (touchMode={touchMode})...");
            if (touchMode)
            {
                Khkt.Input.PadInput.UsingTouch = true;
            }

            var data = new NutInventoryData
            {
                timestamp = DateTime.UtcNow.ToString("s") + "Z",
                gitCommit = touchMode ? "giao-dien-hd" : "ebf7d32b"
            };

            var scenes = new List<string> { LabSceneBuilder.ScenePath };
            string[] khuScenes = Directory.GetFiles("Assets/Scenes/Khu", "*.unity");
            scenes.AddRange(khuScenes);

            var collectedPaths = new HashSet<string>();

            // Hệ số quy đổi đơn vị Canvas (1920x1080 match 0.5) sang pt trên iPad
            float factorAirM3 = Mathf.Sqrt((2360f / 1920f) * (1640f / 1080f)) * 0.5f;
            float factorPro129 = Mathf.Sqrt((2732f / 1920f) * (2048f / 1080f)) * 0.5f;

            foreach (var scenePath in scenes)
            {
                if (!File.Exists(scenePath)) continue;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);

                // Nếu là cảnh chính, kích hoạt sinh động các Canvas giao diện
                if (scenePath == LabSceneBuilder.ScenePath)
                {
                    InstantiateAndInitUiComponents();
                }

                // Quét toàn bộ nút trong cảnh
                var allButtons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var b in allButtons)
                {
                    RecordButton(b.gameObject, "Button", sceneName, collectedPaths, data.buttons, factorAirM3, factorPro129);
                }

                var allTaps = UnityEngine.Object.FindObjectsByType<TouchTapTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var t in allTaps)
                {
                    RecordButton(t.gameObject, "TouchTapTrigger", sceneName, collectedPaths, data.buttons, factorAirM3, factorPro129);
                }

                var allHolds = UnityEngine.Object.FindObjectsByType<TouchHoldTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var h in allHolds)
                {
                    RecordButton(h.gameObject, "TouchHoldTrigger", sceneName, collectedPaths, data.buttons, factorAirM3, factorPro129);
                }
            }

            // Thống kê
            data.totalButtons = data.buttons.Count;
            foreach (var b in data.buttons)
            {
                if (!b.reaches44ptAirM3 || !b.reaches44ptPro129)
                    data.buttonsUnder44pt++;
                if (!string.IsNullOrEmpty(b.shortcutKey))
                    data.buttonsWithKeyboardShortcuts++;
            }

            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../", targetRelPath));
            string json = EditorJsonUtility.ToJson(data, true);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, json);

            Debug.Log($"[UI-INVENTORY] HOÀN TẤT: Đã kiểm kê {data.totalButtons} nút bấm.");
            Debug.Log($"[UI-INVENTORY]   • Nút chưa đạt 44 pt: {data.buttonsUnder44pt}/{data.totalButtons}");
            Debug.Log($"[UI-INVENTORY]   • Nút còn chứa phím tắt PC: {data.buttonsWithKeyboardShortcuts}/{data.totalButtons}");
            Debug.Log($"[UI-INVENTORY] Đã lưu file: {targetRelPath}");

            if (isBatch)
            {
                EditorApplication.Exit(0);
            }
        }

        static void InstantiateAndInitUiComponents()
        {
            var modernUis = UnityEngine.Object.FindObjectsByType<ModernUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in modernUis)
            {
                try { CallMethod(m, "Awake"); } catch (Exception ex) { Debug.LogWarning($"ModernUI.Awake: {ex.InnerException?.Message ?? ex.Message}"); }
            }

            var introScreens = UnityEngine.Object.FindObjectsByType<IntroScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var intro in introScreens)
            {
                try { CallMethod(intro, "Start"); } catch (Exception ex) { Debug.LogWarning($"IntroScreen.Start: {ex.InnerException?.Message ?? ex.Message}"); }
            }

            var touchHuds = UnityEngine.Object.FindObjectsByType<TouchHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var th in touchHuds)
            {
                try { CallMethod(th, "Awake"); } catch (Exception ex) { Debug.LogWarning($"TouchHud.Awake: {ex.InnerException?.Message ?? ex.Message}"); }
            }

            var chemSearches = UnityEngine.Object.FindObjectsByType<ChemSearchUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cs in chemSearches)
            {
                try { CallMethod(cs, "Awake"); } catch (Exception ex) { Debug.LogWarning($"ChemSearchUI.Awake: {ex.InnerException?.Message ?? ex.Message}"); }
            }
        }

        static void CallMethod(object target, string methodName)
        {
            if (target == null) return;
            var m = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null)
            {
                m.Invoke(target, null);
            }
        }

        static void RecordButton(GameObject go, string triggerType, string sceneName, HashSet<string> seen,
                                 List<NutRecord> list, float fAir, float fPro)
        {
            if (go == null) return;
            string path = GetHierarchyPath(go.transform);
            string key = $"{sceneName}:{path}";
            if (seen.Contains(key)) return;
            seen.Add(key);

            var rt = go.GetComponent<RectTransform>();
            Vector2 pos = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector2 size = rt != null ? rt.rect.size : Vector2.zero;
            if ((size.x <= 0f || size.y <= 0f) && rt != null) size = rt.sizeDelta;

            string labelText = "";
            var texts = go.GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
            {
                if (!string.IsNullOrEmpty(t.text))
                {
                    labelText = string.IsNullOrEmpty(labelText) ? t.text : $"{labelText} | {t.text}";
                }
            }

            string shortcut = DetectKeyboardShortcut(labelText);

            float ptW_Air = size.x * fAir;
            float ptH_Air = size.y * fAir;
            float ptW_Pro = size.x * fPro;
            float ptH_Pro = size.y * fPro;

            list.Add(new NutRecord
            {
                scene = sceneName,
                path = path,
                name = go.name,
                triggerType = triggerType,
                posX = Mathf.Round(pos.x * 10f) / 10f,
                posY = Mathf.Round(pos.y * 10f) / 10f,
                width = Mathf.Round(size.x * 10f) / 10f,
                height = Mathf.Round(size.y * 10f) / 10f,
                ptWidth2360 = Mathf.Round(ptW_Air * 10f) / 10f,
                ptHeight2360 = Mathf.Round(ptH_Air * 10f) / 10f,
                ptWidth2732 = Mathf.Round(ptW_Pro * 10f) / 10f,
                ptHeight2732 = Mathf.Round(ptH_Pro * 10f) / 10f,
                reaches44ptAirM3 = ptW_Air >= 44f && ptH_Air >= 44f,
                reaches44ptPro129 = ptW_Pro >= 44f && ptH_Pro >= 44f,
                label = labelText,
                shortcutKey = shortcut,
                hasVisualSublayer = go.transform.Find("Visual") != null || go.transform.Find("Highlight") != null,
                hasSoundFeedback = false
            });
        }

        static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "";
            string p = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                p = t.name + "/" + p;
            }
            return p;
        }

        static readonly Regex ShortcutPattern = new Regex(@"\(([A-Za-z0-9_]+|Space|Enter|Shift|Esc|WASD|Tab|Ctrl)\)", RegexOptions.Compiled);

        static string DetectKeyboardShortcut(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var m = ShortcutPattern.Match(text);
            return m.Success ? m.Groups[1].Value : "";
        }
    }
}
