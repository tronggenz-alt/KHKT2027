using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Khkt.Demo.Bio;
using Khkt.Demo.Instruments;
using Khkt.Environment;
using Khkt.Input;
using Khkt.Interaction;
using Khkt.Menu;
using Khkt.Menu.Dashboard;
using Khkt.Rendering;
using Khkt.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Khkt.Demo
{
    /// <summary>
    /// Bo kiem thu nghiem thu Wave 6 / Step 7 trong Unity PlayMode (batchmode khong -nographics).
    /// </summary>
    public class KhktWave6Runner : MonoBehaviour
    {
        const int ShotW = 1920;
        const int ShotH = 1080;

        string outDir;
        RenderTexture rt;
        Texture2D readTex;
        int passes;
        int failures;
        int runtimeErrors;
        int startupSearchErrors;
        int shotCount;
        readonly List<string> failedNotes = new List<string>();
        readonly List<string> runtimeErrorLines = new List<string>();

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            outDir = System.Environment.GetEnvironmentVariable("KHKT_W6_SHOTS");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "vera_w6_shots");
            Directory.CreateDirectory(outDir);
            Application.runInBackground = true;
            Time.timeScale = 1f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            Application.logMessageReceived += OnUnityLog;
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
            if (readTex != null) { Destroy(readTex); readTex = null; }
        }

        void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (!string.IsNullOrEmpty(stackTrace) && stackTrace.Contains("SearchInit.IndexationOnStartup"))
            {
                startupSearchErrors++;
                return;
            }
            if (!string.IsNullOrEmpty(condition) && condition.StartsWith("[W6_FAIL]")) return;
            runtimeErrors++;
            string line = $"{type}: {condition} | {(stackTrace ?? "").Split('\n').FirstOrDefault()}";
            runtimeErrorLines.Add(line);
            Debug.LogWarning("[W6_RUNTIME_ERR] " + line);
        }

        IEnumerator Start()
        {
            StartCoroutine(Watchdog(360f));

            rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            readTex = new Texture2D(ShotW, ShotH, TextureFormat.RGBA32, false);

            ResetTestPrefs();
            ToolPanel.ForceEnableForTest = true;
            MachinePanel.ForceEnableForTest = true;

            yield return WaitReady();

            // 1. Item 7.8 — IntroScreen Settings: Co chu & Co bang dung cu
            yield return TestIntroSettingsSizes();

            // 2. Titration both click modes (0 & 1) + 7.5 Rubric Result Card
            yield return TestTitrationBothModesAndRubric();

            // 3. Items 7.1, 7.2, 7.6, 7.8 on ToolPanel (Bottle + Buret + Esc + Scaling)
            yield return TestToolPanelItems();

            // 4. Item 7.5 — Blocking Safety Modal + HandController Risk Gate
            yield return TestSafetyModalAndGate();

            // 5. All 14 Concrete InstrumentProgram Classes + Item 7.6 on InstrumentScreen
            yield return TestAll14InstrumentsAndAsk();

            // 6. Item 7.3 — Centrifuge MachinePanel RCF + Presets + Luu moi
            yield return TestCentrifugePresetsAndRcf();

            // 7. Item 7.4 — MicroscopeUI Capture + Gallery Overlay + MicroscopeReportStore
            yield return TestMicroscopeGalleryAndReport();

            // 8. Item 7.7 — ManHinhChinh Guest -> Dashboard PageOverview + Bai tap + Cai thien
            yield return TestDashboardPagesInMainMenu();

            CleanupTestPrefs();

            Debug.Log($"[W6_SUMMARY] passes={passes} failures={failures} runtimeErrors={runtimeErrors} startupSearchIgnored={startupSearchErrors} shots={shotCount}");
            if (failedNotes.Count > 0)
                Debug.LogError("[W6_FAILED_LIST] " + string.Join(" || ", failedNotes));
            if (runtimeErrorLines.Count > 0)
                Debug.LogError("[W6_RUNTIME_ERR_LIST] " + string.Join(" || ", runtimeErrorLines));

            bool ok = failures == 0 && runtimeErrors == 0;
            Debug.Log(ok ? "[W6_RESULT] ALL PASSED" : "[W6_RESULT] FAILED");

#if UNITY_EDITOR
            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }

        IEnumerator Watchdog(float maxSeconds)
        {
            yield return new WaitForSecondsRealtime(maxSeconds);
            Debug.LogError($"[W6_TIMEOUT] Exceeded {maxSeconds:0}s passes={passes} failures={failures} runtimeErrors={runtimeErrors} shots={shotCount}");
#if UNITY_EDITOR
            EditorApplication.Exit(2);
#endif
        }

        static void ResetTestPrefs()
        {
            PlayerPrefs.SetInt(UiTheme.PrefKey, (int)UiTheme.Mode.XanhMuc);
            PlayerPrefs.SetInt(UiTheme.ClickModePrefKey, 0);
            PlayerPrefs.SetInt(UiTheme.FontScalePrefKey, 0);
            PlayerPrefs.SetInt(UiTheme.PanelSizePrefKey, 1);
            PlayerPrefs.DeleteKey("KHKT_WIP_TITLE");
            PlayerPrefs.DeleteKey("KHKT_WIP_STEP");
            PlayerPrefs.DeleteKey("KHKT_WIP_STATION");
            PlayerPrefs.DeleteKey("KHKT_PRESETS_may_ly_tam");
            PlayerPrefs.DeleteKey("KHKT_PRESETS_CentrifugeLoad");
            PlayerPrefs.Save();
            UiTheme.ReloadFromPrefs();
            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            UiTheme.SetFontScale(0);
            UiTheme.SetPanelSize(1);
            MicroscopeReportStore.Clear();
        }

        static void CleanupTestPrefs()
        {
            ResetTestPrefs();
            PlayerPrefs.DeleteKey(MicroscopeReportStore.PrefsKey);
            PlayerPrefs.Save();
        }

        IEnumerator WaitReady()
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 25f)
            {
                if (FindAnyObjectByType<ModernUI>() != null &&
                    FindAnyObjectByType<HandController>() != null &&
                    FindAnyObjectByType<DesktopViewer>() != null &&
                    FindAnyObjectByType<LabProcedure>() != null &&
                    ChemicalCatalog.Instance != null)
                {
                    break;
                }
                yield return null;
            }
            yield return WaitSettled(12f);
            yield return Frames(5);
        }

        IEnumerator WaitSettled(float timeout = 12f)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!ZoneStreamer.Settled && Time.realtimeSinceStartup - t0 < timeout)
                yield return null;
            yield return Frames(3);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator WaitReal(float sec)
        {
            yield return new WaitForSecondsRealtime(sec);
            yield return null;
        }

        void Check(bool cond, string name, string detail = "")
        {
            if (cond)
            {
                passes++;
                Debug.Log($"[W6_PASS] {name}" + (string.IsNullOrEmpty(detail) ? "" : $" | {detail}"));
            }
            else
            {
                failures++;
                string msg = $"[W6_FAIL] {name}" + (string.IsNullOrEmpty(detail) ? "" : $" | {detail}");
                failedNotes.Add(msg);
                Debug.LogError(msg);
            }
        }

        void Shot(Camera cam, string fileBase)
        {
            Canvas.ForceUpdateCanvases();
            if (cam == null)
            {
                Check(false, "Shot camera non-null: " + fileBase);
                return;
            }

            var ui = FindAnyObjectByType<ModernUI>();
            int oldMask = cam.cullingMask;
            Camera overlayCam = null;
            bool overlayOldEnabled = false;
            var swappedCanvases = new List<(Canvas c, Camera cam, float dist)>();

            if (ui != null && cam == ui.viewCamera)
            {
                var ovTr = cam.transform.Find("UI Overlay Camera");
                if (ovTr != null) overlayCam = ovTr.GetComponent<Camera>();
                if (overlayCam != null)
                {
                    overlayOldEnabled = overlayCam.enabled;
                    overlayCam.enabled = false;
                }
                cam.cullingMask |= (1 << 5);
                var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
                float shotDist = Mathf.Max(cam.nearClipPlane + 0.02f, 0.05f);
                for (int i = 0; i < allCanvases.Length; i++)
                {
                    var c = allCanvases[i];
                    if (c == null || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceCamera) continue;
                    if (c.worldCamera == overlayCam || c.worldCamera == cam || c == ui.Canvas)
                    {
                        swappedCanvases.Add((c, c.worldCamera, c.planeDistance));
                        c.worldCamera = cam;
                        c.planeDistance = shotDist;
                    }
                }
                Canvas.ForceUpdateCanvases();
            }

            var old = cam.targetTexture;
            bool oldEnabled = cam.enabled;
            try
            {
                cam.enabled = true;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                readTex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
                readTex.Apply();
                RenderTexture.active = null;
            }
            finally
            {
                cam.targetTexture = old;
                cam.enabled = oldEnabled;
                cam.cullingMask = oldMask;
                for (int i = 0; i < swappedCanvases.Count; i++)
                {
                    var s = swappedCanvases[i];
                    if (s.c == null) continue;
                    s.c.worldCamera = s.cam;
                    s.c.planeDistance = s.dist;
                }
                if (overlayCam != null) overlayCam.enabled = overlayOldEnabled;
                Canvas.ForceUpdateCanvases();
            }

            string path = Path.Combine(outDir, fileBase + ".png");
            File.WriteAllBytes(path, readTex.EncodeToPNG());
            shotCount++;
            Debug.Log($"[W6_SHOT] {path}");
        }

        Camera LabCam()
        {
            var ui = FindAnyObjectByType<ModernUI>();
            if (ui != null && ui.viewCamera != null) return ui.viewCamera;
            var viewer = FindAnyObjectByType<DesktopViewer>();
            return viewer != null && viewer.cameraTransform != null ? viewer.cameraTransform.GetComponent<Camera>() : Camera.main;
        }

        static Button FindButtonByNameOrLabel(Transform root, string exactName, string containsLabel = null)
        {
            if (root == null) return null;
            var btns = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < btns.Length; i++)
            {
                var b = btns[i];
                if (!b.gameObject.activeInHierarchy) continue;
                if (!string.IsNullOrEmpty(exactName) && b.gameObject.name == exactName)
                    return b;
            }
            if (!string.IsNullOrEmpty(containsLabel))
            {
                for (int i = 0; i < btns.Length; i++)
                {
                    var b = btns[i];
                    if (!b.gameObject.activeInHierarchy) continue;
                    var t = b.GetComponentInChildren<Text>(true);
                    if (t != null && t.text != null && t.text.Contains(containsLabel, StringComparison.OrdinalIgnoreCase))
                        return b;
                }
            }
            return null;
        }

        static bool ClickBtn(Transform root, string exactName, string containsLabel = null)
        {
            var b = FindButtonByNameOrLabel(root, exactName, containsLabel);
            if (b == null || !b.interactable) return false;
            b.onClick.Invoke();
            return true;
        }

        static Vector3 AimPointOf(GameObject go)
        {
            var col = go.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.center : go.transform.position + Vector3.up * 0.04f;
        }

        static IEnumerator PressEscKey()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var kb = Keyboard.current != null ? Keyboard.current : InputSystem.AddDevice<Keyboard>();
            kb.MakeCurrent();
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        static int CountScreenOverlaps(IReadOnlyList<RectTransform> rects, out string firstPair)
        {
            firstPair = "";
            int count = 0;
            var cornersA = new Vector3[4];
            var cornersB = new Vector3[4];
            for (int i = 0; i < rects.Count; i++)
            {
                var ra = rects[i];
                if (ra == null || !ra.gameObject.activeInHierarchy) continue;
                ra.GetWorldCorners(cornersA);
                Rect rectA = RectFromCorners(cornersA);
                for (int j = i + 1; j < rects.Count; j++)
                {
                    var rb = rects[j];
                    if (rb == null || !rb.gameObject.activeInHierarchy) continue;
                    rb.GetWorldCorners(cornersB);
                    Rect rectB = RectFromCorners(cornersB);
                    float ox = Mathf.Min(rectA.xMax, rectB.xMax) - Mathf.Max(rectA.xMin, rectB.xMin);
                    float oy = Mathf.Min(rectA.yMax, rectB.yMax) - Mathf.Max(rectA.yMin, rectB.yMin);
                    if (ox > 0.002f && oy > 0.002f)
                    {
                        count++;
                        if (string.IsNullOrEmpty(firstPair))
                            firstPair = $"{ra.name} vs {rb.name}";
                    }
                }
            }
            return count;
        }

        static Rect RectFromCorners(Vector3[] c)
        {
            float minX = Mathf.Min(Mathf.Min(c[0].x, c[1].x), Mathf.Min(c[2].x, c[3].x));
            float maxX = Mathf.Max(Mathf.Max(c[0].x, c[1].x), Mathf.Max(c[2].x, c[3].x));
            float minY = Mathf.Min(Mathf.Min(c[0].y, c[1].y), Mathf.Min(c[2].y, c[3].y));
            float maxY = Mathf.Max(Mathf.Max(c[0].y, c[1].y), Mathf.Max(c[2].y, c[3].y));
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        // =====================================================================
        // 1. Item 7.8 — IntroScreen Settings: Co chu & Co bang dung cu
        // =====================================================================

        IEnumerator TestIntroSettingsSizes()
        {
            var intro = FindAnyObjectByType<IntroScreen>();
            Check(intro != null, "IntroScreen present");
            if (intro == null) yield break;

            UiTheme.Set(UiTheme.Mode.Trang, false);
            intro.Open();
            yield return Frames(3);
            Check(intro.IsOpen, "IntroScreen opens");

            var modernUi = FindAnyObjectByType<ModernUI>();
            var uiCanvas = modernUi != null && modernUi.Canvas != null ? modernUi.Canvas.transform : null;
            bool openedSettings = ClickBtn(uiCanvas, "Cài đặt", "Cài đặt");
            Check(openedSettings, "IntroScreen clicked Cai dat");
            yield return WaitReal(0.28f);

            var btnLon = FindButtonByNameOrLabel(uiCanvas, "Lớn", "Lớn");
            var btn60 = FindButtonByNameOrLabel(uiCanvas, "60%", "60%");
            var btnFull = FindButtonByNameOrLabel(uiCanvas, "Kín màn hình", "Kín màn hình");
            var btnChuan = FindButtonByNameOrLabel(uiCanvas, "Chuẩn", "Chuẩn");
            var btn75 = FindButtonByNameOrLabel(uiCanvas, "75%", "75%");
            Check(btnLon != null && btn60 != null && btnFull != null && btnChuan != null && btn75 != null,
                "IntroScreen Co chu & Co bang dung cu chips present");

            if (btnLon != null)
            {
                float h1 = ((RectTransform)btnLon.transform).sizeDelta.y;
                float h2 = btn60 != null ? ((RectTransform)btn60.transform).sizeDelta.y : 0f;
                Check(h1 >= 44f && h2 >= 44f, "IntroScreen scale chips >= 44 pt", $"hFont={h1:0}, hPanel={h2:0}");
            }

            if (btnLon != null && btn60 != null)
            {
                btnLon.onClick.Invoke();
                btn60.onClick.Invoke();
                yield return Frames(2);
                Check(UiTheme.FontScaleMode == 1 && UiTheme.PanelSizeMode == 0,
                    "IntroScreen scale chips update UiTheme prefs", $"font={UiTheme.FontScaleMode}, panel={UiTheme.PanelSizeMode}");
            }

            if (btnChuan != null) btnChuan.onClick.Invoke();
            if (btn75 != null) btn75.onClick.Invoke();
            yield return WaitReal(0.28f);

            var displayChips = new List<RectTransform>();
            if (btnChuan != null) displayChips.Add((RectTransform)btnChuan.transform);
            if (btnLon != null) displayChips.Add((RectTransform)btnLon.transform);
            if (btn60 != null) displayChips.Add((RectTransform)btn60.transform);
            if (btn75 != null) displayChips.Add((RectTransform)btn75.transform);
            if (btnFull != null) displayChips.Add((RectTransform)btnFull.transform);
            int ov = CountScreenOverlaps(displayChips, out string pair);
            Check(ov == 0, "IntroScreen Display tab scale chips zero overlap", pair);

            Shot(LabCam(), "step7_settings_sizes_Trang");

            intro.Close();
            yield return Frames(3);
            Check(!intro.IsOpen, "IntroScreen closes cleanly");
        }

        // =====================================================================
        // 2. Titration in Both Click Modes (0 & 1) + 7.5 Rubric Result Card
        // =====================================================================

        IEnumerator TestTitrationBothModesAndRubric()
        {
            var viewer = FindAnyObjectByType<DesktopViewer>();
            var hand = FindAnyObjectByType<HandController>();
            var proc = FindAnyObjectByType<LabProcedure>();
            var modern = FindAnyObjectByType<ModernUI>();
            var reading = proc != null && proc.reading != null ? proc.reading : FindAnyObjectByType<MeniscusReading>();

            viewer.GoTo(1);
            yield return WaitSettled(12f);

            var flaskGrab = proc.flask.GetComponent<Grabbable>();
            var bottleGrab = proc.naohStock.GetComponent<Grabbable>();
            var pipGrab = proc.pipette.GetComponent<Grabbable>();
            var buretGrab = proc.buret.GetComponent<Grabbable>();

            // --- Mode 0: Cam ngay ---
            PlayerPrefs.SetInt(UiTheme.ClickModePrefKey, 0);
            proc.Begin(LabProcedure.Config.Default);
            yield return Frames(3);

            // Pick up flask by quick click
            hand.OverrideAimPoint = AimPointOf(flaskGrab.gameObject);
            yield return Frames(3);
            Check(hand.Current == HandController.Act.Pick, "Mode 0 aims flask Pick", $"act={hand.Current}");
            hand.OverrideHold = true;
            yield return null;
            hand.OverrideHold = false;
            yield return Frames(3);
            Check(hand.Held == flaskGrab, "Mode 0 quick click picks flask");

            // Place in underSocket & hold swirl
            hand.PlaceInSocket(proc.underSocket);
            yield return Frames(3);
            hand.OverrideAimPoint = AimPointOf(flaskGrab.gameObject);
            yield return Frames(3);
            Check(hand.Current == HandController.Act.Swirl, "Mode 0 aims flask under buret -> Act.Swirl", $"act={hand.Current}");
            hand.OverrideHold = true;
            yield return WaitReal(0.28f);
            Check(hand.Acting, "Mode 0 Act.Swirl hold swirls");
            hand.OverrideHold = false;
            yield return Frames(2);

            // Click (i) button on hover card -> opens ToolPanel
            hand.OverrideAimPoint = AimPointOf(proc.naohStock.gameObject);
            yield return WaitReal(0.16f);
            bool clickedInfo = ClickBtn(modern.Canvas.transform, "Mở chi tiết");
            yield return Frames(3);
            Check(clickedInfo && ToolPanel.IsOpen, "Mode 0 hover card (i) opens ToolPanel");
            ClickBtn(modern.Canvas.transform, "Đóng");
            yield return Frames(3);

            // Hold 0.4s+ on NaOH bottle -> opens ToolPanel
            hand.OverrideAimPoint = AimPointOf(proc.naohStock.gameObject);
            yield return Frames(3);
            hand.OverrideHold = true;
            float ht0 = Time.realtimeSinceStartup;
            while (!ToolPanel.IsOpen && Time.realtimeSinceStartup - ht0 < 0.85f) yield return null;
            Check(ToolPanel.IsOpen, "Mode 0 0.4s hold opens ToolPanel", $"dt={Time.realtimeSinceStartup - ht0:0.00}s");
            hand.OverrideHold = false;
            hand.OverrideAimPoint = null;
            yield return Frames(2);
            if (ToolPanel.IsOpen) ClickBtn(modern.Canvas.transform, "Đóng");
            if (hand.Held != null) hand.ReturnHome();
            yield return Frames(3);

            // --- Mode 1: Mo bang ---
            PlayerPrefs.SetInt(UiTheme.ClickModePrefKey, 1);
            yield return Frames(2);
            hand.OverrideAimPoint = AimPointOf(proc.naohStock.gameObject);
            yield return Frames(3);
            hand.OverrideHold = true;
            yield return null;
            hand.OverrideHold = false;
            yield return Frames(3);
            Check(ToolPanel.IsOpen, "Mode 1 single click opens bottle ToolPanel");
            bool camLenBottle = ClickBtn(modern.Canvas.transform, "Chính", "Cầm lên");
            yield return Frames(3);
            Check(camLenBottle && !ToolPanel.IsOpen && hand.Held == bottleGrab, "Mode 1 Cam len picks bottle");
            if (ToolPanel.IsOpen) ClickBtn(modern.Canvas.transform, "Đóng");
            if (hand.Held != null) hand.ReturnHome();
            hand.OverrideAimPoint = null;
            yield return Frames(3);

            hand.OverrideAimPoint = AimPointOf(proc.pipette.gameObject);
            yield return Frames(3);
            hand.OverrideHold = true;
            yield return null;
            hand.OverrideHold = false;
            yield return Frames(3);
            Check(ToolPanel.IsOpen, "Mode 1 single click opens pipette ToolPanel");
            bool camLenPip = ClickBtn(modern.Canvas.transform, "Chính", "Cầm lên");
            yield return Frames(3);
            Check(camLenPip && !ToolPanel.IsOpen && hand.Held == pipGrab, "Mode 1 Cam len picks pipette");
            if (ToolPanel.IsOpen) ClickBtn(modern.Canvas.transform, "Đóng");
            if (hand.Held != null) hand.ReturnHome();
            hand.OverrideAimPoint = null;
            yield return Frames(2);

            // --- Complete full titration to verify LabProcedure + 7.5 Rubric Card ---
            PlayerPrefs.SetInt(UiTheme.ClickModePrefKey, 0);
            proc.Begin(LabProcedure.Config.Default);
            yield return Frames(4);

            hand.Pick(buretGrab);
            hand.PlaceInSocket(proc.clampSocket);
            yield return Frames(3);

            hand.Pick(bottleGrab);
            hand.OverrideAimPoint = proc.buret.container.mouth.position + Vector3.down * 0.03f;
            viewer.LookAt(proc.buret.container.mouth.position);
            yield return Frames(6);
            hand.OverrideHold = true;
            Time.timeScale = 4f;
            float tf = Time.realtimeSinceStartup;
            while (proc.buret.ReadingMl > -1.0f && Time.realtimeSinceStartup - tf < 15f) yield return null;
            hand.OverrideHold = false;
            Time.timeScale = 1f;
            hand.OverrideAimPoint = null;
            hand.ReturnHome();
            yield return Frames(3);

            var wasteGrab = proc.wasteBeaker.GetComponent<Grabbable>();
            hand.Pick(wasteGrab);
            hand.PlaceInSocket(proc.underSocket);
            proc.buret.AdjustOpening(0.6f);
            Time.timeScale = 3f;
            tf = Time.realtimeSinceStartup;
            while (proc.buret.ReadingMl < 0.25f && Time.realtimeSinceStartup - tf < 12f) yield return null;
            proc.buret.Close();
            Time.timeScale = 1f;
            hand.Pick(wasteGrab);
            hand.ReturnHome();
            yield return Frames(3);

            yield return LevelEye(viewer, reading);
            proc.RecordReading();
            viewer.EyeOffset = 0f;
            Check(proc.InitialRead.HasValue, "Titration recorded InitialRead V dau", $"V0={proc.InitialRead}");

            hand.Pick(pipGrab);
            hand.OverrideAimPoint = proc.hclStock.transform.position + Vector3.up * 0.05f;
            yield return Frames(5);
            hand.OverrideHold = true;
            Time.timeScale = 3f;
            tf = Time.realtimeSinceStartup;
            while (proc.pipette.OffsetFromMarkMm <= 4f && Time.realtimeSinceStartup - tf < 15f) yield return null;
            hand.OverrideHold = false;
            Time.timeScale = 1f;
            yield return Frames(3);
            hand.OverrideShift = true;
            hand.OverrideHold = true;
            tf = Time.realtimeSinceStartup;
            while (proc.pipette.OffsetFromMarkMm > 0.2f && Time.realtimeSinceStartup - tf < 15f) yield return null;
            hand.OverrideHold = false;
            hand.OverrideShift = false;
            hand.OverrideAimPoint = proc.flask.transform.position + Vector3.up * 0.05f;
            yield return Frames(3);
            hand.OverrideHold = true;
            Time.timeScale = 3f;
            tf = Time.realtimeSinceStartup;
            while (!proc.pipette.IsEmpty && Time.realtimeSinceStartup - tf < 15f) yield return null;
            hand.OverrideHold = false;
            Time.timeScale = 1f;
            hand.OverrideAimPoint = null;
            hand.ReturnHome();
            yield return Frames(3);

            proc.indicatorDropper.DropInto(proc.flask);
            proc.indicatorDropper.DropInto(proc.flask);
            hand.Pick(flaskGrab);
            hand.PlaceInSocket(proc.underSocket);
            viewer.LookAt(proc.buret.tip.position + Vector3.down * 0.04f);
            yield return Frames(5);

            PadInput.TouchSwirl = true;
            proc.buret.AdjustOpening(0.9f);
            Time.timeScale = 5f;
            tf = Time.realtimeSinceStartup;
            while (proc.NaohIntoFlaskMl < proc.EquivalenceMl - 1.5f && Time.realtimeSinceStartup - tf < 18f) yield return null;
            proc.buret.Close();
            proc.buret.AdjustOpening(0.38f);
            Time.timeScale = 6f;
            tf = Time.realtimeSinceStartup;
            while (proc.flask.PinkPersistS <= 0.6f && proc.NaohIntoFlaskMl <= proc.EquivalenceMl + 1f && Time.realtimeSinceStartup - tf < 15f) yield return null;
            proc.buret.Close();
            Time.timeScale = 8f;
            tf = Time.realtimeSinceStartup;
            while (proc.EndpointPersistS <= 31f && Time.realtimeSinceStartup - tf < 12f) yield return null;
            Time.timeScale = 1f;
            PadInput.TouchSwirl = false;

            yield return LevelEye(viewer, reading);
            proc.RecordReading();
            viewer.EyeOffset = 0f;
            viewer.LookAt(proc.flask.transform.position + Vector3.up * 0.15f);
            yield return WaitReal(0.3f);

            Check(proc.LastResult.HasValue, "Titration produced LastResult",
                proc.LastResult.HasValue ? $"err={proc.LastResult.Value.errorPercent:0.00}%" : proc.Message);

            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            yield return WaitReal(0.32f);

            var resTr = modern.Canvas.transform.Find("Kết quả");
            if (resTr == null) resTr = modern.Canvas.transform.Find("Bảng kết quả");
            if (resTr == null)
            {
                var textsAll = modern.Canvas.GetComponentsInChildren<Text>(true);
                var rubTitle = textsAll.FirstOrDefault(t => t.gameObject.activeInHierarchy && t.text.Contains("1. Chuẩn bị"));
                resTr = rubTitle != null ? rubTitle.transform.parent.parent : null;
            }
            var rubricTexts = modern.Canvas.GetComponentsInChildren<Text>(true)
                .Where(t => t.gameObject.activeInHierarchy &&
                       (t.text.Contains("1. Chuẩn bị") || t.text.Contains("2. Kỹ thuật đọc vạch") ||
                        t.text.Contains("3. Độ chính xác điểm tương đương") || t.text.Contains("4. An toàn")))
                .ToArray();
            Check(resTr != null && rubricTexts.Length >= 4, "7.5 Rubric Result Card visible with 4 real criteria", $"rows={rubricTexts.Length}");

            Shot(LabCam(), "step7_rubric_result_XanhMuc");

            ClickBtn(modern.Canvas.transform, "Làm lượt mới", "lượt mới");
            proc.NewRun();
            proc.Stop();
            yield return Frames(3);
        }

        static IEnumerator LevelEye(DesktopViewer viewer, MeniscusReading reading)
        {
            if (viewer == null || reading == null) yield break;
            float baseEye = viewer.seated ? viewer.seatedEyeHeight : viewer.standingEyeHeight;
            viewer.EyeOffset = reading.MeniscusPosition.y - viewer.transform.position.y - baseEye;
            viewer.LookAt(reading.MeniscusPosition);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        // =====================================================================
        // 3. Items 7.1, 7.2, 7.6, 7.8 on ToolPanel
        // =====================================================================

        IEnumerator TestToolPanelItems()
        {
            var viewer = FindAnyObjectByType<DesktopViewer>();
            var modern = FindAnyObjectByType<ModernUI>();
            var proc = FindAnyObjectByType<LabProcedure>();

            viewer.GoTo(2);
            yield return WaitSettled(12f);

            ChemicalBottle bottle = FindObjectsByType<ChemicalBottle>()
                .Where(b => b != null && b.gameObject.activeInHierarchy && b.container != null && b.container.VolumeMl > 20f &&
                            ToolPanel.GetStockMolarity(b, b.Entry) >= 0.5f)
                .OrderBy(b => Vector3.Distance(b.transform.position, viewer.transform.position))
                .FirstOrDefault();
            if (bottle == null && proc.naohStock != null)
            {
                bottle = proc.naohStock.GetComponent<ChemicalBottle>();
                if (bottle == null) bottle = proc.naohStock.GetComponentInParent<ChemicalBottle>();
            }
            if (bottle == null)
                bottle = FindObjectsByType<ChemicalBottle>().FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy && b.container != null);

            var nearContainers = FindObjectsByType<LabContainer>()
                .Where(c => c != null && c.gameObject.activeInHierarchy && c.GetComponentInParent<ChemicalBottle>() == null &&
                            Vector3.Distance(bottle.transform.position, c.transform.position) <= 6f)
                .OrderBy(c => Vector3.Distance(bottle.transform.position, c.transform.position))
                .Take(3)
                .ToArray();
            var cat = ChemicalCatalog.Instance;
            if (nearContainers.Length >= 2 && cat != null)
            {
                var water = cat.Search("nước cất", 1).FirstOrDefault();
                var naoh = cat.Get(124);
                var cuso4 = cat.Get(253);
                if (nearContainers[0].VolumeMl < 1f && naoh != null) nearContainers[0].SetContents(cat.Make(naoh, 15f, 1f));
                if (nearContainers[1].VolumeMl < 1f && cuso4 != null) nearContainers[1].SetContents(cat.Make(cuso4, 15f, 0.5f));
                if (nearContainers.Length > 2 && nearContainers[2].VolumeMl < 1f && water != null) nearContainers[2].SetContents(cat.Make(water, 20f));
            }

            var snapBefore = nearContainers.Select(c => (c, vol: c.VolumeMl, ph: c.BulkPh, temp: c.TemperatureC)).ToArray();
            float bottleBeforeVol = bottle.container.VolumeMl;

            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            ToolPanel.OpenBottle(bottle);
            yield return WaitReal(0.32f);
            Check(ToolPanel.IsOpen, "ToolPanel opened for bottle", bottle.DisplayName);

            var predTexts = modern.Canvas.GetComponentsInChildren<Text>(true)
                .Where(t => t.gameObject.activeInHierarchy && t.transform.parent != null && t.transform.parent.name.StartsWith("Dòng dự đoán"))
                .ToArray();
            Check(predTexts.Length >= 2, "7.1 Mix prediction rows rendered", $"textFields={predTexts.Length}");

            var c2Chip = FindButtonByNameOrLabel(modern.Canvas.transform, "Nồng độ đích 0");
            var v2Chip = FindButtonByNameOrLabel(modern.Canvas.transform, "Thể tích đích 2");
            if (v2Chip == null) v2Chip = FindButtonByNameOrLabel(modern.Canvas.transform, "Thể tích đích 0");
            if (c2Chip != null) c2Chip.onClick.Invoke();
            if (v2Chip != null) v2Chip.onClick.Invoke();
            yield return Frames(2);

            var formulaLbl = modern.Canvas.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(t => t.gameObject.activeInHierarchy && t.gameObject.name == "Công thức V1");
            Check(formulaLbl != null && formulaLbl.text.Contains("Lấy V₁ =") && !formulaLbl.text.Contains("NaN"),
                "7.2 Dilution formula computed cleanly", formulaLbl != null ? formulaLbl.text : "null");

            var slider = modern.Canvas.GetComponentsInChildren<Slider>(true).FirstOrDefault(s => s.gameObject.activeInHierarchy);
            float sliderBefore = slider != null ? slider.value : -1f;
            bool applied = ClickBtn(modern.Canvas.transform, "Áp dụng V1");
            yield return Frames(2);
            float sliderAfter = slider != null ? slider.value : -1f;
            Check(applied && sliderAfter > 0f && !float.IsNaN(sliderAfter),
                "7.2 Ap dung V1 updated pour slider", $"before={sliderBefore:0.#}, after={sliderAfter:0.#}");

            ClickBtn(modern.Canvas.transform, "Nhận 1");
            yield return Frames(2);

            bool mutated = false;
            foreach (var s in snapBefore)
            {
                if (Mathf.Abs(s.c.VolumeMl - s.vol) > 1e-4f ||
                    Mathf.Abs((float)s.c.BulkPh - (float)s.ph) > 1e-4f ||
                    Mathf.Abs((float)s.c.TemperatureC - (float)s.temp) > 1e-4f)
                    mutated = true;
            }
            if (Mathf.Abs(bottle.container.VolumeMl - bottleBeforeVol) > 1e-4f) mutated = true;
            Check(!mutated, "7.1 Isolated ChemEngine simulation did NOT mutate real containers or bottle");

            yield return WaitReal(0.28f);
            Shot(LabCam(), "step7_bottle_dilution_XanhMuc");

            UiTheme.Set(UiTheme.Mode.Trang, false);
            yield return WaitReal(0.32f);
            Shot(LabCam(), "step7_bottle_dilution_Trang");

            // 7.6 Ask Assistant inside ToolPanel
            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            yield return WaitReal(0.28f);
            var tpWin = modern.Canvas.transform.Find("Bảng chi tiết dụng cụ");
            bool asked = ClickBtn(tpWin, "Hỏi trợ lý", "Hỏi trợ lý");
            yield return WaitReal(0.35f);
            var chat = RobotChatUI.Instance != null ? RobotChatUI.Instance : FindAnyObjectByType<RobotChatUI>(FindObjectsInactive.Include);
            var chatGo = modern.Canvas.transform.Find("Trợ lý AI")?.gameObject;
            int tpSib = tpWin != null ? tpWin.GetSiblingIndex() : -1;
            int chatSib = chatGo != null ? chatGo.transform.GetSiblingIndex() : -1;
            Check(asked && ToolPanel.IsOpen && chat != null && chat.IsOpen && chatSib > tpSib,
                "7.6 ToolPanel Hoi tro ly opens RobotChatUI frontmost without closing ToolPanel",
                $"tpSib={tpSib}, chatSib={chatSib}");
            Shot(LabCam(), "step7_ask_assistant_XanhMuc");

            yield return PressEscKey();
            yield return Frames(2);
            Check(chat != null && !chat.IsOpen && ToolPanel.IsOpen,
                "7.6 Esc closes RobotChatUI first while keeping ToolPanel open");
            if (chat != null && chat.IsOpen) chat.Close();

            ClickBtn(tpWin != null ? tpWin : modern.Canvas.transform, "Đóng");
            yield return Frames(3);

            // 7.8 ToolPanel scaling at 60% & Kin man hinh
            viewer.GoTo(1);
            yield return WaitSettled(10f);
            if (proc.buret.container.VolumeMl < 5f && ChemicalCatalog.Instance != null)
            {
                var naoh = ChemicalCatalog.Instance.Get(124);
                if (naoh != null) proc.buret.container.SetContents(ChemicalCatalog.Instance.Make(naoh, 35f, 0.1f));
            }
            ToolPanel.OpenBuret(proc.buret);
            yield return WaitReal(0.3f);

            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            UiTheme.SetPanelSize(0);
            yield return WaitReal(0.32f);
            var win60 = modern.Canvas.transform.Find("Bảng chi tiết dụng cụ") as RectTransform;
            float s60 = win60 != null ? win60.localScale.x : 0f;
            float w60 = win60 != null ? win60.sizeDelta.x * s60 : 0f;
            float h60 = win60 != null ? win60.sizeDelta.y * s60 : 0f;
            Check(Mathf.Abs(s60 - 0.88f) < 0.03f && w60 <= 1920f && h60 <= 1080f,
                "7.8 ToolPanel 60% size scale fits cleanly", $"scale={s60:0.000}, size={w60:0.0}x{h60:0.0}");
            Shot(LabCam(), "step7_panel_60_XanhMuc");

            UiTheme.Set(UiTheme.Mode.Den, false);
            UiTheme.SetPanelSize(2);
            yield return WaitReal(0.32f);
            var winFull = modern.Canvas.transform.Find("Bảng chi tiết dụng cụ") as RectTransform;
            float sFull = winFull != null ? winFull.localScale.x : 0f;
            float wFull = winFull != null ? winFull.sizeDelta.x * sFull : 0f;
            float hFull = winFull != null ? winFull.sizeDelta.y * sFull : 0f;
            Check(sFull > 1.05f && wFull <= 1920f && hFull <= 1080f,
                "7.8 ToolPanel Kin man hinh size scale fits <= 1920x1080", $"scale={sFull:0.000}, size={wFull:0.0}x{hFull:0.0}");
            Shot(LabCam(), "step7_panel_full_Den");

            UiTheme.SetPanelSize(1);
            var winAfter = modern.Canvas.transform.Find("Bảng chi tiết dụng cụ");
            ClickBtn(winAfter != null ? winAfter : modern.Canvas.transform, "Đóng");
            yield return Frames(3);
            Check(!ToolPanel.IsOpen && DesktopViewer.UiCapture == 0, "ToolPanel closed cleanly with UiCapture=0");
        }

        // =====================================================================
        // 4. Item 7.5 — Blocking Safety Modal & HandController Risk Gate
        // =====================================================================

        IEnumerator TestSafetyModalAndGate()
        {
            var hand = FindAnyObjectByType<HandController>();
            var modern = FindAnyObjectByType<ModernUI>();
            var viewer = FindAnyObjectByType<DesktopViewer>();
            viewer.GoTo(1);
            yield return WaitSettled(8f);

            var washBottle = FindObjectsByType<WashBottle>(FindObjectsInactive.Exclude).FirstOrDefault();
            GameObject concAcidGo = new GameObject("TestConcH2SO4Beaker");
            var col = concAcidGo.AddComponent<BoxCollider>();
            col.size = new Vector3(0.14f, 0.16f, 0.14f);
            var cont = concAcidGo.AddComponent<LabContainer>();
            cont.capacityMl = 100f;
            cont.label = "Cốc H₂SO₄ đặc thử";
            var cat = ChemicalCatalog.Instance;
            var h2so4 = cat != null ? cat.Search("H2SO4", 5).FirstOrDefault(e => e.C >= 10f) : null;
            if (h2so4 != null) cont.SetContents(cat.Make(h2so4, 25f, 18f));
            else cont.SetContents(Solution.Of(25f, Khkt.Simulation.Chem.Mix.Of(("SO4", 0.45))));

            var cam = LabCam().transform;
            concAcidGo.transform.position = cam.position + cam.forward * 0.65f + Vector3.down * 0.15f;

            if (washBottle != null)
            {
                hand.Pick(washBottle.GetComponent<Grabbable>());
                hand.OverrideAimPoint = concAcidGo.transform.position;
                yield return Frames(3);
                hand.OverrideHold = true;
                yield return Frames(2);
                hand.OverrideHold = false;
                hand.OverrideAimPoint = null;
            }

            bool gatedByHand = ModernUI.IsSafetyModalOpen;
            Check(gatedByHand, "7.5 HandController TryGateUnsafeAction blocked water into conc H2SO4");
            if (!ModernUI.IsSafetyModalOpen)
            {
                ModernUI.ShowSafetyWarning(
                    "Nguy cơ sôi bắn axit H₂SO₄ đặc!",
                    "Đổ nước hoặc dung dịch loãng vào axit sunfuric đặc (> 6 M) tỏa nhiệt cực mạnh, dễ làm dung dịch sôi bắn gây bỏng.",
                    () => { },
                    () => { });
                yield return Frames(2);
            }

            UiTheme.Set(UiTheme.Mode.Trang, false);
            yield return WaitReal(0.28f);
            Check(ModernUI.IsSafetyModalOpen && DesktopViewer.UiCapture > 0,
                "7.5 Safety modal open & capturing input");

            Shot(LabCam(), "step7_safety_modal_Trang");

            bool cancelBtn = ClickBtn(modern.Canvas.transform, "Nút đổi cách làm");
            yield return Frames(3);
            Check(cancelBtn && !ModernUI.IsSafetyModalOpen && !hand.Acting,
                "7.5 Doi cach lam aborts dangerous action cleanly");
            hand.ReturnHome();
            Destroy(concAcidGo);

            bool proceeded = false, changed = false;
            ModernUI.ShowSafetyWarning("Thử nghiệm an toàn", "Xác minh nút Vẫn làm hoạt động đúng.",
                () => proceeded = true, () => changed = true);
            yield return Frames(2);
            bool okProceedBtn = ClickBtn(modern.Canvas.transform, "Nút vẫn làm");
            yield return Frames(2);
            Check(okProceedBtn && proceeded && !changed && !ModernUI.IsSafetyModalOpen && DesktopViewer.UiCapture == 0,
                "7.5 Van lam executes onProceed and releases capture cleanly");
        }

        // =====================================================================
        // 5. All 14 Concrete InstrumentProgram Classes + Item 7.6 Esc Check
        // =====================================================================

        IEnumerator TestAll14InstrumentsAndAsk()
        {
            var viewer = FindAnyObjectByType<DesktopViewer>();
            var modern = FindAnyObjectByType<ModernUI>();
            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            UiTheme.SetFontScale(0);
            UiTheme.SetPanelSize(1);

            viewer.GoTo(7);
            yield return WaitSettled(12f);

            var hosts = FindObjectsByType<AnalyzerInstrument>(FindObjectsInactive.Include);
            var programs = new (AnalyzerInstrument.Kind kind, InstrumentProgram prog)[]
            {
                (AnalyzerInstrument.Kind.Hplc, new HplcProgram()),
                (AnalyzerInstrument.Kind.UvVis, new UvVisProgram()),
                (AnalyzerInstrument.Kind.Distiller, new DistillerProgram()),
                (AnalyzerInstrument.Kind.Autoclave, new AutoclaveProgram()),
                (AnalyzerInstrument.Kind.Aas, new AasProgram()),
                (AnalyzerInstrument.Kind.Calorimeter, new CalorimeterProgram()),
                (AnalyzerInstrument.Kind.Sem, new SemProgram()),
                (AnalyzerInstrument.Kind.Xrd, new XrdProgram()),
                (AnalyzerInstrument.Kind.AutoTitrator, new TitratorProgram()),
                (AnalyzerInstrument.Kind.Ftir, new FtirProgram()),
                (AnalyzerInstrument.Kind.DryingOven, new DryingOvenProgram()),
                (AnalyzerInstrument.Kind.Furnace, new FurnaceProgram()),
                (AnalyzerInstrument.Kind.Nmr, new NmrProgram()),
                (AnalyzerInstrument.Kind.GcMs, new GcMsProgram()),
            };

            string[] regions = { "Thanh trên", "Quy trình", "Sơ đồ", "Đồ thị", "Điều khiển" };
            int instOkCount = 0;

            for (int i = 0; i < programs.Length; i++)
            {
                var (kind, prog) = programs[i];
                var host = hosts.FirstOrDefault(h => h != null && h.kind == kind);
                InstrumentScreen.Open(host, prog, null);
                yield return Frames(2);

                var instRoot = modern.Canvas.transform.Find("Giao diện màn hình máy");
                var winFrame = instRoot != null ? instRoot.Find("Khung màn hình máy") as RectTransform : null;
                bool hasAll5 = winFrame != null && regions.All(r =>
                {
                    var ch = winFrame.Find(r);
                    return ch != null && ch.gameObject.activeInHierarchy;
                });
                float winScale = winFrame != null ? winFrame.localScale.x : 0f;
                bool camOn = modern.viewCamera != null && modern.viewCamera.enabled;

                int minScaledPt = 999;
                string minLbl = "";
                if (winFrame != null)
                {
                    var allTxt = winFrame.GetComponentsInChildren<Text>(false);
                    for (int t = 0; t < allTxt.Length; t++)
                    {
                        var tx = allTxt[t];
                        if (!tx.gameObject.activeInHierarchy || string.IsNullOrEmpty(tx.text)) continue;
                        int eff = Mathf.RoundToInt(tx.fontSize * winScale);
                        if (eff < minScaledPt) { minScaledPt = eff; minLbl = $"{tx.name}:{tx.fontSize}"; }
                    }
                }

                bool rowOk = InstrumentScreen.IsOpen && camOn && hasAll5 && Mathf.Abs(winScale - 0.865f) < 0.01f && minScaledPt >= 12;
                if (rowOk) instOkCount++;
                Check(rowOk, $"Instrument[{i + 1}/14] {prog.GetType().Name}",
                    $"open={InstrumentScreen.IsOpen}, cam={camOn}, regions={hasAll5}, scale={winScale:0.000}, minPt={minScaledPt} ({minLbl})");

                if (i == 1) // UvVisProgram: test 7.6 Hoi tro ly + Esc priority
                {
                    bool clickedAsk = ClickBtn(winFrame, "Hỏi trợ lý", "Hỏi trợ lý");
                    yield return WaitReal(0.32f);
                    var chat = RobotChatUI.Instance != null ? RobotChatUI.Instance : FindAnyObjectByType<RobotChatUI>(FindObjectsInactive.Include);
                    var chatGo = modern.Canvas.transform.Find("Trợ lý AI")?.gameObject;
                    int instSib = instRoot != null ? instRoot.GetSiblingIndex() : -1;
                    int chatSib = chatGo != null ? chatGo.transform.GetSiblingIndex() : -1;
                    Check(clickedAsk && chat != null && chat.IsOpen && chatSib > instSib,
                        "7.6 InstrumentScreen Hoi tro ly opens RobotChatUI above InstrumentScreen",
                        $"instSib={instSib}, chatSib={chatSib}");

                    yield return PressEscKey();
                    yield return Frames(2);
                    Check(chat != null && !chat.IsOpen && InstrumentScreen.IsOpen,
                        "7.6 Esc closes RobotChatUI first without closing InstrumentScreen");
                }

                InstrumentScreen.Instance.Close();
                yield return Frames(1);
            }

            Check(instOkCount == 14, "All 14 Concrete InstrumentProgram classes passed", $"{instOkCount}/14");
        }

        // =====================================================================
        // 6. Item 7.3 — Centrifuge MachinePanel RCF + Presets + Luu moi
        // =====================================================================

        IEnumerator TestCentrifugePresetsAndRcf()
        {
            var viewer = FindAnyObjectByType<DesktopViewer>();
            var modern = FindAnyObjectByType<ModernUI>();
            CentrifugeLoad cent = null;
            foreach (int st in new[] { 25, 28 })
            {
                viewer.GoTo(st);
                yield return WaitSettled(12f);
                cent = FindObjectsByType<CentrifugeLoad>(FindObjectsInactive.Exclude).FirstOrDefault();
                if (cent != null) break;
            }
            Check(cent != null, "CentrifugeLoad found in streamed bio zone");
            if (cent == null) yield break;

            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            MachinePanel.OpenForTest(cent);
            yield return WaitReal(0.35f);
            Check(MachinePanel.IsOpen, "MachinePanel opened for CentrifugeLoad");

            var panelTr = modern.Canvas.transform.Find("Bảng máy");
            bool hasRcfText = panelTr != null && panelTr.GetComponentsInChildren<Text>(true)
                .Any(t => t.gameObject.activeInHierarchy && t.text.Contains("×g"));
            Check(hasRcfText, "7.3 Live RCF (xg) readout visible");

            var unitBtn = FindButtonByNameOrLabel(panelTr, "Đơn vị Tốc độ quay", "×g");
            var speedSlider = panelTr != null ? panelTr.GetComponentsInChildren<Slider>(true).FirstOrDefault(s => s.gameObject.activeInHierarchy) : null;
            float beforeVal = speedSlider != null ? speedSlider.value : 0f;
            if (unitBtn != null) unitBtn.onClick.Invoke();
            yield return Frames(2);
            float altVal = speedSlider != null ? speedSlider.value : 0f;
            Check(unitBtn != null && !float.IsNaN(altVal) && Mathf.Abs(altVal - beforeVal) > 10f,
                "7.3 Toggle RPM <-> xg converts slider cleanly", $"rpm={beforeVal:0}, xg={altVal:0}");
            if (unitBtn != null) unitBtn.onClick.Invoke();
            yield return Frames(2);

            bool appliedBuiltIn = ClickBtn(panelTr, "CT Tủa ADN");
            yield return Frames(2);
            bool savedCustom = ClickBtn(panelTr, "Lưu mới");
            yield return WaitReal(0.28f);
            var customChip = FindButtonByNameOrLabel(panelTr, "CT Tùy chỉnh 1", "Tùy chỉnh 1");
            bool hasSavedPref = PlayerPrefs.HasKey("KHKT_PRESETS_may_ly_tam") ||
                                PlayerPrefs.HasKey("KHKT_PRESETS_CentrifugeLoad") ||
                                !string.IsNullOrEmpty(PlayerPrefs.GetString("KHKT_PRESETS_may_ly_tam_de_ban", "")) ||
                                !string.IsNullOrEmpty(PlayerPrefs.GetString("KHKT_PRESETS_may_ly_tam_mini", ""));
            Check(appliedBuiltIn && savedCustom && customChip != null,
                "7.3 Built-in preset applied & Luu moi created Tuy chinh 1 chip", $"prefSaved={hasSavedPref}");

            Shot(LabCam(), "step7_machine_presets_XanhMuc");

            ClickBtn(panelTr, "Đóng");
            yield return Frames(2);
        }

        // =====================================================================
        // 7. Item 7.4 — MicroscopeUI Snapshot Gallery & Report Export
        // =====================================================================

        IEnumerator TestMicroscopeGalleryAndReport()
        {
            var viewer = FindAnyObjectByType<DesktopViewer>();
            var modern = FindAnyObjectByType<ModernUI>();
            viewer.GoTo(21);
            yield return WaitSettled(12f);

            var mic = FindObjectsByType<MicroscopeController>(FindObjectsInactive.Include)
                .OrderBy(m => Vector3.Distance(m.transform.position, viewer.transform.position))
                .FirstOrDefault();
            Check(mic != null, "MicroscopeController found at station 21");
            if (mic == null) yield break;

            mic.LoadSlideById("bieu_bi_hanh");
            mic.SetObjective(2); // 40x -> 400x
            UiTheme.Set(UiTheme.Mode.Trang, false);
            MicroscopeReportStore.Clear();
            MicroscopeUI.Open(mic);
            yield return WaitReal(0.35f);
            Check(MicroscopeUI.IsOpen, "MicroscopeUI opened cleanly");

            var micRoot = modern.Canvas.transform.Find("Kính hiển vi");
            bool captured = ClickBtn(micRoot, "Chụp ảnh");
            yield return Frames(3);
            var galBtn = FindButtonByNameOrLabel(micRoot, "Ảnh đã chụp");
            string galTxt = galBtn != null ? (galBtn.GetComponentInChildren<Text>()?.text ?? "") : "";
            Check(captured && galTxt.Contains("(1)"), "7.4 Chup anh increments Anh da chup (1)", galTxt);

            bool addedReport = ClickBtn(micRoot, "Thêm vào báo cáo");
            yield return Frames(2);
            Check(addedReport && MicroscopeReportStore.Count >= 1, "7.4 Them vao bao cao records entry", $"count={MicroscopeReportStore.Count}");

            if (galBtn != null) galBtn.onClick.Invoke();
            yield return WaitReal(0.35f);

            var galOverlay = micRoot != null ? micRoot.Find("Xem ảnh đã chụp") : null;
            bool galVisible = galOverlay != null && galOverlay.gameObject.activeInHierarchy;
            bool hasThumbTex = galVisible && galOverlay.GetComponentsInChildren<RawImage>(true)
                .Any(ri => ri.gameObject.activeInHierarchy && ri.texture != null);
            Check(galVisible && hasThumbTex, "7.4 Gallery overlay visible with captured Texture2D thumbnail");

            Shot(LabCam(), "step7_microscope_gallery_Trang");

            ClickBtn(micRoot, "Đóng thư viện trên", "Đóng");
            yield return Frames(2);
            ClickBtn(micRoot, "Đóng");
            yield return Frames(2);
        }

        // =====================================================================
        // 8. Item 7.7 — ManHinhChinh Guest -> Dashboard PageOverview / Bai tap / Cai thien
        // =====================================================================

        IEnumerator TestDashboardPagesInMainMenu()
        {
            PlayerPrefs.DeleteKey("KHKT_WIP_TITLE");
            PlayerPrefs.DeleteKey("KHKT_WIP_STEP");
            PlayerPrefs.DeleteKey("KHKT_WIP_STATION");
            PlayerPrefs.Save();
            GameSession.GuestActive = false;

            SceneManager.LoadScene("ManHinhChinh");
            float t0 = Time.realtimeSinceStartup;
            while (FindAnyObjectByType<MainMenu>() == null && Time.realtimeSinceStartup - t0 < 20f)
                yield return null;
            yield return Frames(5);

            var menu = FindAnyObjectByType<MainMenu>();
            Check(menu != null && menu.menuCamera != null, "ManHinhChinh loaded with MainMenu");
            if (menu == null) yield break;

            UiTheme.Set(UiTheme.Mode.Trang, false);
            yield return WaitReal(0.25f);

            bool enteredGuest = ClickBtn(menu.transform, "Nút Dùng thử");
            yield return WaitReal(0.35f);
            var dash = FindAnyObjectByType<DashboardShell>();
            Check(enteredGuest && GameSession.GuestActive && dash != null && dash.gameObject.activeInHierarchy,
                "7.7 Guest entered DashboardShell cleanly");

            var wipTexts = dash.GetComponentsInChildren<Text>(true)
                .Where(t => t.gameObject.activeInHierarchy && (t.text.Contains("Bài đang làm dở") || t.text.Contains("Chưa có bài làm dở")))
                .ToArray();
            Check(wipTexts.Length >= 2, "7.7 PageOverview renders honest Bai dang lam do card", $"hits={wipTexts.Length}");

            dash.Go("bai_tap");
            yield return WaitReal(0.35f);
            var assignBtns = dash.GetComponentsInChildren<Button>(true)
                .Where(b => b.gameObject.activeInHierarchy && (b.GetComponentInChildren<Text>()?.text ?? "").Contains("Làm bài"))
                .ToArray();
            Check(assignBtns.Length >= 6 && assignBtns.All(b => ((RectTransform)b.transform).sizeDelta.y >= 44f),
                "7.7 Bai tap page renders 6 real VERA assignments with >=44pt buttons", $"cards={assignBtns.Length}");
            Shot(menu.menuCamera, "step7_dashboard_assignments_Trang");

            dash.Go("cai_thien");
            yield return WaitReal(0.35f);
            bool hasHonestEmpty = dash.GetComponentsInChildren<Text>(true)
                .Any(t => t.gameObject.activeInHierarchy && t.text.Contains("Chưa có dữ liệu điểm cá nhân"));
            var skillBtns = dash.GetComponentsInChildren<Button>(true)
                .Where(b => b.gameObject.activeInHierarchy && (b.GetComponentInChildren<Text>()?.text ?? "").Contains("Thực hành ngay"))
                .ToArray();
            Check(hasHonestEmpty && skillBtns.Length >= 3 && skillBtns.All(b => ((RectTransform)b.transform).sizeDelta.y >= 44f),
                "7.7 Cai thien page shows honest empty notice & 3 core skill practice cards", $"skills={skillBtns.Length}");
            Shot(menu.menuCamera, "step7_dashboard_improvement_Trang");
        }
    }
}

