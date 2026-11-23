using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Khkt.Avatar;
using Khkt.Demo;
using Khkt.Demo.Bio;
using Khkt.Demo.Bio.GiaiPhau;
using Khkt.Demo.Bio.Portal;
using Khkt.Demo.Bio.ThucVat;
using Khkt.Environment;
using Khkt.Input;
using Khkt.Interaction;
using Khkt.Network;
using Khkt.Rendering;
using Khkt.Simulation.Bio.ThucVat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Kiểm thử và chụp ảnh nghiệm thu chuỗi [G2] – [G4] ngày 18/11/2026.
    /// Chạy thuần trong Editor PlayMode để tách biệt hoàn toàn khỏi bản dựng phát hành.
    /// </summary>
    [InitializeOnLoad]
    public static class KhktN18Verify
    {
        const string PrefCmd = "VERA_N18_VERIFY_CMD";
        const int ShotW = 1600;
        const int ShotH = 1000;

        static IEnumerator activeRoutine;
        static readonly Stack<IEnumerator> callStack = new Stack<IEnumerator>();
        static float deadlineRealtime;

        static int passes;
        static int failures;
        static int runtimeErrors;
        static readonly List<string> failedNotes = new List<string>();
        static readonly List<string> runtimeErrNotes = new List<string>();

        static KhktN18Verify()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            TryStartPendingCommand();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                TryStartPendingCommand();
        }

        static void TryStartPendingCommand()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return;
            string cmd = EditorPrefs.GetString(PrefCmd, "");
            if (string.IsNullOrEmpty(cmd)) return;

            EditorPrefs.DeleteKey(PrefCmd);
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            callStack.Clear();
            deadlineRealtime = Time.realtimeSinceStartup + 220f;
            passes = failures = runtimeErrors = 0;
            failedNotes.Clear();
            runtimeErrNotes.Clear();

            if (cmd == "G2_TRUOC")
                activeRoutine = CoCaptureG2Baseline();
            else if (cmd == "G2_RUN")
                activeRoutine = CoRunG2Verify(exitWhenDone: true);
            else if (cmd == "G3_RUN")
                activeRoutine = CoRunG3Verify(exitWhenDone: true);
            else if (cmd == "G4_RUN")
                activeRoutine = CoRunG4Verify(exitWhenDone: true);
            else if (cmd == "ALL_RUN")
                activeRoutine = CoRunAllVerify();
        }

        public static void ChupG2Truoc() => LaunchPlayModeCommand("G2_TRUOC");
        public static void RunG2() => LaunchPlayModeCommand("G2_RUN");
        public static void RunG3() => LaunchPlayModeCommand("G3_RUN");
        public static void RunG4() => LaunchPlayModeCommand("G4_RUN");
        public static void Run() => LaunchPlayModeCommand("ALL_RUN");

        static IEnumerator CoRunAllVerify()
        {
            yield return CoRunG2Verify(exitWhenDone: false);
            yield return CoRunG3Verify(exitWhenDone: false);
            yield return CoRunG4Verify(exitWhenDone: true);
        }

        static void EnsureMatCatResourceMaterial()
        {
            const string resDir = "Assets/Scripts/Demo/Bio/GiaiPhau/Resources";
            const string matPath = resDir + "/MatCatGiaiPhauMat.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) return;
            var shader = Shader.Find("Khkt/Bio/MatCatGiaiPhau");
            if (shader == null) return;
            Directory.CreateDirectory(resDir);
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }

        static void LaunchPlayModeCommand(string cmd)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[N18] Đang ở PlayMode, hãy gọi từ Batchmode/EditMode.");
                return;
            }
            EnsureMatCatResourceMaterial();
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PhongThiNghiem.unity");
            if (!scene.IsValid())
            {
                Debug.LogError("[N18_FATAL] Không mở được Assets/Scenes/PhongThiNghiem.unity");
                EditorApplication.Exit(1);
                return;
            }

            foreach (var s in UnityEngine.Object.FindObjectsByType<SerialControllerSource>(FindObjectsInactive.Include))
                UnityEngine.Object.DestroyImmediate(s);

            EditorPrefs.SetString(PrefCmd, cmd);
            EditorApplication.EnterPlaymode();
        }

        static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (condition != null && (condition.StartsWith("[N18") || condition.StartsWith("Curl error"))) return;
            runtimeErrors++;
            if (runtimeErrNotes.Count < 8)
                runtimeErrNotes.Add($"{condition} | {stackTrace}");
        }

        static void Check(bool cond, string name, string detail = "")
        {
            if (cond)
            {
                passes++;
                Debug.Log($"[N18_PASS] {name}" + (string.IsNullOrEmpty(detail) ? "" : $" | {detail}"));
            }
            else
            {
                failures++;
                string msg = $"[N18_FAIL] {name}" + (string.IsNullOrEmpty(detail) ? "" : $" | {detail}");
                failedNotes.Add(msg);
                Debug.LogError(msg);
            }
        }

        static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying) return;

            if (Time.realtimeSinceStartup > deadlineRealtime)
            {
                EditorApplication.update -= OnEditorUpdate;
                Application.logMessageReceived -= OnUnityLog;
                Debug.LogError("[N18_TIMEOUT] Hết thời gian chờ khi chạy KhktN18Verify.");
                EditorApplication.Exit(2);
                return;
            }

            if (activeRoutine == null)
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            try
            {
                var top = callStack.Count > 0 ? callStack.Peek() : activeRoutine;
                if (top.MoveNext())
                {
                    if (top.Current is IEnumerator nested)
                        callStack.Push(nested);
                }
                else
                {
                    if (callStack.Count > 0) callStack.Pop();
                    else
                    {
                        activeRoutine = null;
                        EditorApplication.update -= OnEditorUpdate;
                    }
                }
            }
            catch (Exception e)
            {
                activeRoutine = null;
                callStack.Clear();
                EditorApplication.update -= OnEditorUpdate;
                Application.logMessageReceived -= OnUnityLog;
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static IEnumerator WaitPlayFrames(int count)
        {
            int target = Time.frameCount + Mathf.Max(1, count);
            while (Time.frameCount < target) yield return null;
        }

        static IEnumerator WaitSeconds(float sec)
        {
            float until = Time.realtimeSinceStartup + sec;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        static IEnumerator WaitSceneReady()
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 25f)
            {
                if (UnityEngine.Object.FindAnyObjectByType<DesktopViewer>() != null &&
                    UnityEngine.Object.FindAnyObjectByType<HandController>() != null &&
                    UnityEngine.Object.FindAnyObjectByType<ModernUI>() != null)
                    break;
                yield return null;
            }
            yield return WaitPlayFrames(15);

            var intro = UnityEngine.Object.FindAnyObjectByType<IntroScreen>();
            if (intro != null && intro.IsOpen) intro.Close();

            var viewer = UnityEngine.Object.FindAnyObjectByType<DesktopViewer>();
            if (viewer != null) viewer.GoTo(2);
            yield return WaitPlayFrames(4);

            float tSettle = Time.realtimeSinceStartup;
            while (!ZoneStreamer.Settled && Time.realtimeSinceStartup - tSettle < 12f)
                yield return null;
            yield return WaitSeconds(0.4f);
            yield return WaitPlayFrames(10);
        }

        static string GetG2ShotDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "ngay-18-11", "anh", "g2"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        static string GetG3ShotDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "ngay-18-11", "anh", "g3"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // =====================================================================
        // Nhóm G2 — Chụp hiện trạng & Nghiệm thu sau sửa
        // =====================================================================

        static IEnumerator CoCaptureG2Baseline()
        {
            yield return WaitSceneReady();

            var viewer = UnityEngine.Object.FindAnyObjectByType<DesktopViewer>();
            var hand = HandController.Active != null ? HandController.Active : UnityEngine.Object.FindAnyObjectByType<HandController>();
            Camera mainCam = hand != null && hand.viewCamera != null ? hand.viewCamera : Camera.main;

            if (viewer == null || hand == null || hand.hand == null || mainCam == null)
            {
                Debug.LogError("[N18_FATAL] Thiếu DesktopViewer / HandController / HandRig / Camera.");
                EditorApplication.Exit(1);
                yield break;
            }

            string outDir = GetG2ShotDir();
            var sideCamGo = new GameObject("N18_SideCam");
            var sideCam = CreateAuxCamera(sideCamGo, mainCam.cullingMask);
            var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);

            Grabbable flask = FindGrabbableByKind("binh_non");
            Pipette pip = UnityEngine.Object.FindAnyObjectByType<Pipette>();
            Grabbable pipGrab = pip != null ? pip.GetComponent<Grabbable>() : null;
            BuretStopcock stopcock = UnityEngine.Object.FindAnyObjectByType<BuretStopcock>();

            Vector3 basePos = viewer.transform.position;
            float baseYaw = viewer.transform.eulerAngles.y;
            Renderer[] handRenderers = hand.hand.GetComponentsInChildren<Renderer>(true);

            try
            {
                ResetViewer(viewer, hand, basePos, baseYaw, 18f);
                hand.OverrideAimPoint = AimInFront(mainCam, 0.44f, -0.06f, 0.06f);
                yield return WaitPlayFrames(18);
                ForceBaselineCurl(hand.hand, 0f, handRenderers);
                CapturePair(mainCam, sideCam, rt, tex, outDir, "truoc_1_xoe", hand.hand.transform, handRenderers);

                hand.OverrideAimPoint = AimInFront(mainCam, 0.44f, -0.06f, 0.06f);
                yield return WaitPlayFrames(10);
                ForceBaselineCurl(hand.hand, 0.85f, handRenderers);
                CapturePair(mainCam, sideCam, rt, tex, outDir, "truoc_2_chum_cam", hand.hand.transform, handRenderers);

                if (flask != null)
                {
                    if (hand.Held != null) hand.ReturnHome();
                    hand.Pick(flask);
                    hand.OverrideAimPoint = AimInFront(mainCam, 0.46f, -0.08f, 0.05f);
                    yield return WaitPlayFrames(24);
                    ForceRenderersVisible(handRenderers, true);
                    CapturePair(mainCam, sideCam, rt, tex, outDir, "truoc_3_binh_non", hand.hand.transform, handRenderers);
                    hand.ReturnHome();
                    yield return WaitPlayFrames(6);
                }

                if (pipGrab != null)
                {
                    if (hand.Held != null) hand.ReturnHome();
                    hand.Pick(pipGrab);
                    hand.OverrideAimPoint = AimInFront(mainCam, 0.44f, -0.06f, 0.05f);
                    yield return WaitPlayFrames(24);
                    ForceRenderersVisible(handRenderers, true);
                    CapturePair(mainCam, sideCam, rt, tex, outDir, "truoc_4_pipet", hand.hand.transform, handRenderers);
                    hand.ReturnHome();
                    yield return WaitPlayFrames(6);
                }

                if (stopcock != null)
                {
                    PositionInFrontOfStopcock(viewer, hand, basePos, stopcock);
                    yield return WaitPlayFrames(18);
                    ForceBaselineCurl(hand.hand, 0.65f, handRenderers);
                    CapturePair(mainCam, sideCam, rt, tex, outDir, "truoc_5_khoa_buret", hand.hand.transform, handRenderers);
                }

                ResetViewer(viewer, hand, basePos, baseYaw, 14f);
                hand.OverrideAimPoint = AimInFront(mainCam, 0.50f, -0.04f, 0.08f);
                yield return WaitPlayFrames(18);
                ForceBaselineCurl(hand.hand, 0.25f, handRenderers);
                CapturePair(mainCam, sideCam, rt, tex, outDir, "truoc_6_chi_tay", hand.hand.transform, handRenderers);

                hand.OverrideAimPoint = null;
                Debug.Log($"[N18_G2_TRUOC] Hoàn tất chụp 12 ảnh hiện trạng vào {outDir}");
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(sideCamGo);
            }

            EditorApplication.Exit(0);
        }

        static IEnumerator CoRunG2Verify(bool exitWhenDone)
        {
            Application.logMessageReceived += OnUnityLog;
            yield return WaitSceneReady();

            HandRig.WearGloves = true;
            LocalPlayerBody.Visible = true;
            HardwareProfile.DatTuDong();

            var viewer = UnityEngine.Object.FindAnyObjectByType<DesktopViewer>();
            var hand = HandController.Active != null ? HandController.Active : UnityEngine.Object.FindAnyObjectByType<HandController>();
            var armRig = UnityEngine.Object.FindAnyObjectByType<ArmRig>();
            Camera mainCam = hand != null && hand.viewCamera != null ? hand.viewCamera : Camera.main;

            Check(viewer != null && hand != null && hand.hand != null && mainCam != null, "G2 Scene components ready");
            if (viewer == null || hand == null || hand.hand == null || mainCam == null)
            {
                FinishVerify(exitWhenDone);
                yield break;
            }

            HandRig rig = hand.hand;
            rig.EnsureBuilt();
            var skinShader = Shader.Find("Khkt/DaNguoi");
            Check(skinShader != null, "G2 Shader Khkt/DaNguoi compiled");

            // 1. Giải phẫu bàn tay & móng tay
            bool monoDesc = true;
            for (int f = 0; f < 4; f++)
            {
                var lens = HandRig.FingerBoneLengths(f);
                if (!(lens.prox > lens.mid && lens.mid >= lens.dist)) monoDesc = false;
                if (!rig.GetFingerJoints(f, out var m, out var p, out var d, out var t) || m == null || p == null || d == null || t == null)
                    monoDesc = false;
            }
            rig.GetThumbJoints(out var tc, out var tm, out var ti, out var tt);
            Check(monoDesc && tc != null && tm != null && ti != null && tt != null, "G2 Anatomy 3-phalanx fingers + 3-segment thumb");
            Check(rig.FingerNailsCount == 5, "G2 Five fingernails built", $"count={rig.FingerNailsCount}");
            Check(rig.WristAttach != null, "G2 WristAttach bridge ready");

            // 2. Giới hạn góc khớp sinh lý (không gập ngược)
            var badFinger = HandPoseTable.ClampFinger(new FingerPose(-40f, 80f, -20f, 120f));
            var badThumb = HandPoseTable.ClampThumb(new ThumbPose(-30f, -15f, 95f, -25f, 110f));
            bool limitsOk = badFinger.mcpFlex >= 0f && badFinger.pipFlex >= 0f && badFinger.dipFlex >= 0f &&
                            badFinger.dipFlex <= HandPoseTable.FingerDipMaxFlex &&
                            badThumb.cmcOppose >= HandPoseTable.ThumbCmcMinOppose &&
                            badThumb.mcpFlex >= 0f && badThumb.ipFlex >= 0f;
            Check(limitsOk, "G2 Physiological joint angle limits enforced");

            // Chuyển mượt 0,15 s + AvatarSettingsHook trong Tab Điều khiển
            Check(Mathf.Approximately(HandPoseTable.TransitionDurationS, 0.15f), "G2 Pose transition duration is 0.15s");
            AvatarSettingsHook.EnsureHook();
            yield return WaitPlayFrames(6);
            var intro = UnityEngine.Object.FindAnyObjectByType<IntroScreen>();
            Transform uiRoot = intro != null && intro.ui != null && intro.ui.Canvas != null ? intro.ui.Canvas.transform : null;
            Transform gloveTgl = uiRoot != null ? FindChildRecursive(uiRoot, "ToggleDeoGangTay") : null;
            Transform bodyTgl = uiRoot != null ? FindChildRecursive(uiRoot, "ToggleHienThanMinh") : null;
            Check(gloveTgl != null && bodyTgl != null, "G2 Settings Display/Controls toggles injected in Tab Dieu khien");

            string outDir = GetG2ShotDir();
            var sideCamGo = new GameObject("N18_SideCam");
            var sideCam = CreateAuxCamera(sideCamGo, mainCam.cullingMask);
            var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);

            Grabbable flask = FindGrabbableByKind("binh_non");
            Pipette pip = UnityEngine.Object.FindAnyObjectByType<Pipette>();
            Grabbable pipGrab = pip != null ? pip.GetComponent<Grabbable>() : null;
            BuretStopcock stopcock = UnityEngine.Object.FindAnyObjectByType<BuretStopcock>();

            Vector3 basePos = viewer.transform.position;
            float baseYaw = viewer.transform.eulerAngles.y;
            Renderer[] handRenderers = rig.GetComponentsInChildren<Renderer>(true);

            GameObject tempPresenceGo = null;
            try
            {
                // 3. Chụp 6 tư thế sau sửa ở đúng góc với truoc_*.png & kiểm tra IK chống xuyên vật
                // (1) Xòe
                ResetViewer(viewer, hand, basePos, baseYaw, 18f);
                rig.PoseOverride = HandPoseId.Xoe;
                hand.OverrideAimPoint = AimInFront(mainCam, 0.42f, -0.06f, 0.06f);
                yield return WaitSeconds(0.22f);
                yield return WaitPlayFrames(4);
                rig.StepPoseAndContact(HandPoseTable.TransitionDurationS);
                CheckConnectedWrist(armRig, rig, "G2 Wrist connected to sleeve (Xoe)");
                CapturePair(mainCam, sideCam, rt, tex, outDir, "sau_1_xoe", rig.transform, handRenderers);

                // (2) Chụm cầm
                rig.PoseOverride = HandPoseId.ChumCam;
                hand.OverrideAimPoint = AimInFront(mainCam, 0.42f, -0.06f, 0.06f);
                yield return WaitSeconds(0.22f);
                yield return WaitPlayFrames(4);
                rig.StepPoseAndContact(HandPoseTable.TransitionDurationS);
                CapturePair(mainCam, sideCam, rt, tex, outDir, "sau_2_chum_cam", rig.transform, handRenderers);
                rig.PoseOverride = null;

                // (3) Cầm bình nón
                if (flask != null)
                {
                    if (hand.Held != null) hand.ReturnHome();
                    hand.Pick(flask);
                    hand.OverrideAimPoint = AimInFront(mainCam, 0.44f, -0.08f, 0.05f);
                    yield return WaitSeconds(0.25f);
                    yield return WaitPlayFrames(6);
                    rig.StepPoseAndContact(HandPoseTable.TransitionDurationS);
                    Check(rig.LastPenetrationCount == 0 && rig.LastMaxSurfaceErrorMm <= 3.0f,
                        "G2 Binh non non-penetrating surface IK <= 3 mm",
                        $"pen={rig.LastPenetrationCount}, err={rig.LastMaxSurfaceErrorMm:0.00}mm");
                    CapturePair(mainCam, sideCam, rt, tex, outDir, "sau_3_binh_non", rig.transform, handRenderers);
                    hand.ReturnHome();
                    yield return WaitPlayFrames(6);
                }
                else
                {
                    Check(false, "G2 Found Binh non for pose verification");
                }

                // (4) Cầm pipet
                if (pipGrab != null)
                {
                    if (hand.Held != null) hand.ReturnHome();
                    hand.Pick(pipGrab);
                    hand.OverrideAimPoint = AimInFront(mainCam, 0.42f, -0.06f, 0.05f);
                    yield return WaitSeconds(0.25f);
                    yield return WaitPlayFrames(6);
                    rig.StepPoseAndContact(HandPoseTable.TransitionDurationS);
                    Check(rig.LastPenetrationCount == 0 && rig.LastMaxSurfaceErrorMm <= 3.0f,
                        "G2 Pipet non-penetrating surface IK <= 3 mm",
                        $"pen={rig.LastPenetrationCount}, err={rig.LastMaxSurfaceErrorMm:0.00}mm");
                    CapturePair(mainCam, sideCam, rt, tex, outDir, "sau_4_pipet", rig.transform, handRenderers);
                    hand.ReturnHome();
                    yield return WaitPlayFrames(6);
                }
                else
                {
                    Check(false, "G2 Found Pipet for pose verification");
                }

                // (5) Vặn khóa buret
                if (stopcock != null)
                {
                    PositionInFrontOfStopcock(viewer, hand, basePos, stopcock);
                    rig.PoseOverride = HandPoseId.KhoaBuret;
                    rig.TargetContactObject = stopcock.transform;
                    yield return WaitSeconds(0.25f);
                    yield return WaitPlayFrames(6);
                    rig.StepPoseAndContact(HandPoseTable.TransitionDurationS);
                    Check(rig.LastPenetrationCount == 0 && rig.LastMaxSurfaceErrorMm <= 3.0f,
                        "G2 Khoa buret non-penetrating surface IK <= 3 mm",
                        $"pen={rig.LastPenetrationCount}, err={rig.LastMaxSurfaceErrorMm:0.00}mm");
                    CapturePair(mainCam, sideCam, rt, tex, outDir, "sau_5_khoa_buret", rig.transform, handRenderers);
                    rig.PoseOverride = null;
                    rig.TargetContactObject = null;
                }
                else
                {
                    Check(false, "G2 Found BuretStopcock for pose verification");
                }

                // (6) Chỉ tay
                ResetViewer(viewer, hand, basePos, baseYaw, 14f);
                rig.PoseOverride = HandPoseId.ChiTay;
                hand.OverrideAimPoint = AimInFront(mainCam, 0.46f, -0.04f, 0.08f);
                yield return WaitSeconds(0.22f);
                yield return WaitPlayFrames(4);
                rig.StepPoseAndContact(HandPoseTable.TransitionDurationS);
                CapturePair(mainCam, sideCam, rt, tex, outDir, "sau_6_chi_tay", rig.transform, handRenderers);
                rig.PoseOverride = null;

                // (7) Tháo găng tay -> chụp da thật Khkt/DaNguoi
                HandRig.WearGloves = false;
                rig.PoseOverride = HandPoseId.Xoe;
                yield return WaitPlayFrames(6);
                rig.StepPoseAndContact(HandPoseTable.TransitionDurationS);
                Vector3 pivotSkin = rig.transform.position + rig.transform.forward * 0.03f;
                sideCam.transform.position = pivotSkin + mainCam.transform.right * 0.22f + mainCam.transform.forward * 0.14f + Vector3.up * 0.07f;
                sideCam.transform.LookAt(pivotSkin - mainCam.transform.forward * 0.02f, Vector3.up);
                RenderToFile(sideCam, rt, tex, Path.Combine(outDir, "sau_9_da_khong_gang.png"), includeUi: false);
                HandRig.WearGloves = true;
                rig.PoseOverride = null;
                hand.OverrideAimPoint = null;

                // 8. Ánh xạ 21 điểm MediaPipe vào HandRig
                var fakeLm = BuildSyntheticPinchLandmarks();
                InAppHandTracker.MapLandmarksToHandRig(fakeLm, rig, mainCam.transform.rotation);
                rig.GetFingerJoints(0, out var idxMcp, out var idxPip, out _, out _);
                Check(idxMcp.localEulerAngles.x >= 10f && idxPip.localEulerAngles.x >= 10f,
                    "G2 MediaPipe 21-point landmark mapping drives HandRig",
                    $"mcp={idxMcp.localEulerAngles.x:0.0} pip={idxPip.localEulerAngles.x:0.0}");
                rig.ClearLandmarkControl();

                // 9. Mô hình người 3D + 3 mức LOD + Phòng học chung giả lập 1 máy
                ResetViewer(viewer, hand, basePos, baseYaw, 4f);
                yield return WaitPlayFrames(4);
                tempPresenceGo = new GameObject("N18_SimLabPresence");
                var presence = tempPresenceGo.AddComponent<LabPresence>();
                yield return WaitPlayFrames(2);

                Vector3 camFwd = new Vector3(mainCam.transform.forward.x, 0f, mainCam.transform.forward.z).normalized;
                Vector3 camRight = new Vector3(mainCam.transform.right.x, 0f, mainCam.transform.right.z).normalized;
                Vector3 teacherHead = new Vector3(basePos.x, 0f, basePos.z) + camFwd * 2.15f - camRight * 0.42f + Vector3.up * 1.54f;
                Vector3 studentHead = new Vector3(basePos.x, 0f, basePos.z) + camFwd * 2.15f + camRight * 0.42f + Vector3.up * 1.54f;

                var teacherAv = presence.SpawnSimulatedRemote(9001, "GV. Nguyễn An", true,
                    teacherHead, baseYaw + 180f, 4f,
                    teacherHead - camRight * 0.22f - camFwd * 0.32f - Vector3.up * 0.26f,
                    Quaternion.LookRotation(-camFwd, Vector3.up));
                var studentAv = presence.SpawnSimulatedRemote(9002, "HS. Minh Châu", false,
                    studentHead, baseYaw + 170f, 6f,
                    studentHead - camRight * 0.20f - camFwd * 0.30f - Vector3.up * 0.28f,
                    Quaternion.LookRotation(-camFwd, Vector3.up));
                yield return WaitPlayFrames(6);

                Check(presence.RemoteHeadOf(9001, out _, out string rName) && rName == "GV. Nguyễn An",
                    "G2 LabPresence spawns simulated HumanoidAvatar + RemoteHeadOf");

                HardwareProfile.Dat(MucDoHoa.Cao);
                teacherAv.UpdateLod(mainCam.transform.position);
                int lodCao = teacherAv.CurrentLod;
                HardwareProfile.Dat(MucDoHoa.Vua);
                teacherAv.UpdateLod(mainCam.transform.position);
                int lodVua = teacherAv.CurrentLod;
                HardwareProfile.Dat(MucDoHoa.Thap);
                teacherAv.UpdateLod(mainCam.transform.position);
                int lodThap = teacherAv.CurrentLod;
                HardwareProfile.DatTuDong();
                teacherAv.UpdateLod(mainCam.transform.position);
                studentAv.UpdateLod(mainCam.transform.position);
                Check(lodCao == 0 && lodVua == 1 && lodThap == 2,
                    "G2 HumanoidAvatar supports 3 LOD levels driven by HardwareProfile",
                    $"cao={lodCao}, vua={lodVua}, thap={lodThap}");

                yield return WaitPlayFrames(4);
                RenderToFile(mainCam, rt, tex, Path.Combine(outDir, "sau_7_phong_hoc_chung.png"), includeUi: true);

                // 10. Thân của chính mình + bật/tắt
                UnityEngine.Object.Destroy(tempPresenceGo);
                tempPresenceGo = null;
                yield return WaitPlayFrames(2);
                LocalPlayerBody.Visible = false;
                bool hiddenOk = !LocalPlayerBody.Visible;
                LocalPlayerBody.Visible = true;
                Check(hiddenOk && LocalPlayerBody.Visible && LocalPlayerBody.Instance != null, "G2 LocalPlayerBody attaches and toggles cleanly");

                ResetViewer(viewer, hand, basePos, baseYaw, 68f);
                yield return WaitPlayFrames(8);
                RenderToFile(mainCam, rt, tex, Path.Combine(outDir, "sau_8_than_minh.png"), includeUi: false);
                ResetViewer(viewer, hand, basePos, baseYaw, 10f);
            }
            finally
            {
                if (tempPresenceGo != null) UnityEngine.Object.Destroy(tempPresenceGo);
                HandRig.WearGloves = true;
                LocalPlayerBody.Visible = true;
                HardwareProfile.DatTuDong();
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(sideCamGo);
                Application.logMessageReceived -= OnUnityLog;
            }

            FinishVerify(exitWhenDone, "G2");
        }

        // =====================================================================
        // Nhóm G3 — Mô hình 3D thực vật: cấu trúc, chức năng, dạy học
        // =====================================================================

        static IEnumerator CoRunG3Verify(bool exitWhenDone)
        {
            Application.logMessageReceived -= OnUnityLog;
            Application.logMessageReceived += OnUnityLog;
            yield return WaitSceneReady();

            // 1. Unit test C# thuần
            bool simOk = ThucVatSimTests.RunAll(out int simPassed, out int simFailed, out _);
            Check(simOk && simFailed == 0 && simPassed >= 10, "G3 Pure C# ThucVatSimTests passed", $"pass={simPassed}, fail={simFailed}");

            // 2. Tài liệu thông số có nguồn khoa học data/sinh/thuc_vat.json
            string jsonPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "data", "sinh", "thuc_vat.json"));
            bool jsonValid = File.Exists(jsonPath) && File.ReadAllText(jsonPath).Contains("\"nguon\"");
            Check(jsonValid, "G3 data/sinh/thuc_vat.json exists with cited sources", jsonPath);

            // 3. Lối vào Bàn mô hình thực vật tại Nhà kính tầng 2
            var viewer = UnityEngine.Object.FindAnyObjectByType<DesktopViewer>();
            Camera mainCam = Camera.main;
            if (viewer != null)
            {
                viewer.SetPose(new Vector3(20.2f, 3.8f, -5.0f), 68f, 15f);
                Physics.SyncTransforms();
            }
            float tZone = Time.realtimeSinceStartup;
            while (!ZoneStreamer.Settled && Time.realtimeSinceStartup - tZone < 10f) yield return null;
            yield return WaitPlayFrames(15);

            var ban = BanMoHinhThucVat.EnsureSpawned();
            yield return WaitPlayFrames(2);
            Check(ban != null && ban.gameObject.activeInHierarchy, "G3 Greenhouse BanMoHinhThucVat active in scene");

            string outDir = GetG3ShotDir();
            var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);

            try
            {
                if (mainCam != null)
                    RenderToFile(mainCam, rt, tex, Path.Combine(outDir, "g3_6_ban_mo_hinh_nha_kinh.png"), includeUi: false);

                // 4. Mở mô-đun qua hợp đồng Khkt.Bio.ThucVat.ThucVatMoDun tìm từ AppDomain (như C4) và từ bàn nhà kính
                Type contractType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    contractType = asm.GetType("Khkt.Bio.ThucVat.ThucVatMoDun", false);
                    if (contractType != null) break;
                }
                bool contractOk = false;
                if (contractType != null)
                {
                    var pDangMo = contractType.GetProperty("DangMo");
                    var mMo = contractType.GetMethod("Mo", Type.EmptyTypes);
                    var mDong = contractType.GetMethod("Dong", Type.EmptyTypes);
                    mMo?.Invoke(null, null);
                    bool openedViaContract = pDangMo != null && (bool)pDangMo.GetValue(null) && ThucVatMoDunImpl.DangMo;
                    mDong?.Invoke(null, null);
                    bool closedViaContract = pDangMo != null && !(bool)pDangMo.GetValue(null) && !ThucVatMoDunImpl.DangMo;
                    contractOk = openedViaContract && closedViaContract;
                }
                Check(contractOk, "G3 Khkt.Bio.ThucVat.ThucVatMoDun contract discovered and working across assemblies");

                HardwareProfile.Dat(MucDoHoa.Cao);
                ban?.Use(null);
                yield return WaitPlayFrames(8);

                var ctrl = ThucVatMoDunImpl.HienTai;
                Check(ThucVatMoDunImpl.DangMo && ctrl != null && ctrl.Cay3D != null && ctrl.StageCamera != null,
                    "G3 ThucVatMoDunImpl.Mo() opens dedicated 3D stage and UI");
                if (ctrl == null) yield break;

                // 5. Kéo thanh trượt kiểm tra đúng chiều sinh lý
                var baseEnv = DieuKienMoiTruong.MacDinh;
                ctrl.DatDieuKien(baseEnv);
                ctrl.SetAnhSang(1500f); double qhThap = ctrl.TrangThai.CuongDoQuangHopRong;
                ctrl.SetAnhSang(18000f); double qhVua = ctrl.TrangThai.CuongDoQuangHopRong;
                ctrl.SetAnhSang(50000f); double qhCao = ctrl.TrangThai.CuongDoQuangHopRong;
                ctrl.SetAnhSang(0f); double qhDem = ctrl.TrangThai.CuongDoQuangHopRong;
                bool lightOk = qhVua > qhThap && qhCao >= qhVua && (qhCao - qhVua) < (qhVua - qhThap) && ctrl.TrangThai.BanDem && qhDem < 0.0;
                Check(lightOk, "G3 Photosynthesis increases to saturation and night stops photosynthesis",
                    $"low={qhThap:F2}, mid={qhVua:F2}, sat={qhCao:F2}, night={qhDem:F2}");

                ctrl.DatDieuKien(baseEnv);
                ctrl.SetDoAmKhongKhi(25f); double th25 = ctrl.TrangThai.TocDoThoatHoiNuocMmPhut;
                ctrl.SetDoAmKhongKhi(88f); double th88 = ctrl.TrangThai.TocDoThoatHoiNuocMmPhut;
                ctrl.SetDoAmDat(12f); double turgorDry = ctrl.TrangThai.DoTruongTeBaoHatDau01;
                ctrl.SetDoAmDat(80f); double turgorWet = ctrl.TrangThai.DoTruongTeBaoHatDau01;
                Check(th88 < th25 && turgorDry < turgorWet * 0.5,
                    "G3 Humidity reduces transpiration and drought ABA closes guard cells",
                    $"rh25={th25:F2}, rh88={th88:F2}, turgorDry={turgorDry:F2}, wet={turgorWet:F2}");

                ctrl.SetGocDen(55f);
                ctrl.SetSoGioHuongSang(36f);
                Check(ctrl.TrangThai.GocUonThanDo > 30.0 && ctrl.TrangThai.TiLeAuxinPhiaToi > 0.60,
                    "G3 Phototropism bends stem toward light with auxin accumulation on dark side",
                    $"bend={ctrl.TrangThai.GocUonThanDo:F1}deg");

                // 6. Chụp 5 ảnh cấu trúc, 3 lát cắt và chuyển động dạy học
                ctrl.ChonCheDo(CheDoThucVat.BaiGiang);
                ctrl.DatDieuKien(DieuKienMoiTruong.MacDinh);
                ctrl.ChonKhungNhin(KhungNhinThucVat.TongThe, instantCamera: true);
                ctrl.ChonBoPhan("than_cay");
                yield return WaitPlayFrames(6);
                RenderStageWithOverlay(ctrl.StageCamera, ctrl.UiCanvas, rt, tex, Path.Combine(outDir, "g3_1_tong_the_cay_hai_la_mam.png"));

                ctrl.ChonKhungNhin(KhungNhinThucVat.LatCatRe, instantCamera: true);
                ctrl.ChonBoPhan("re_dai_caspari");
                yield return WaitPlayFrames(6);
                RenderStageWithOverlay(ctrl.StageCamera, ctrl.UiCanvas, rt, tex, Path.Combine(outDir, "g3_2_lat_cat_re_caspari.png"));

                ctrl.ChonKhungNhin(KhungNhinThucVat.LatCatThan, instantCamera: true);
                ctrl.ChonBoPhan("than_tang_sinh_mach");
                yield return WaitPlayFrames(6);
                RenderStageWithOverlay(ctrl.StageCamera, ctrl.UiCanvas, rt, tex, Path.Combine(outDir, "g3_3_lat_cat_than_bo_mach.png"));

                ctrl.ChonKhungNhin(KhungNhinThucVat.LatCatLa, instantCamera: true);
                ctrl.SetDoAmDat(85f);
                ctrl.ChonBoPhan("la_khi_khong");
                yield return WaitPlayFrames(6);
                RenderStageWithOverlay(ctrl.StageCamera, ctrl.UiCanvas, rt, tex, Path.Combine(outDir, "g3_4_lat_cat_la_khi_khong.png"));

                ctrl.ChonCheDo(CheDoThucVat.TuDo);
                ctrl.ChonKhungNhin(KhungNhinThucVat.TongThe, instantCamera: true);
                ctrl.SetAnhSang(36000f);
                ctrl.SetGocDen(55f);
                ctrl.SetSoGioHuongSang(42f);
                ctrl.ChonBoPhan("la_cay");
                yield return WaitPlayFrames(6);
                RenderStageWithOverlay(ctrl.StageCamera, ctrl.UiCanvas, rt, tex, Path.Combine(outDir, "g3_5_quang_hop_va_huong_sang.png"));

                // 6B. Chụp kiểm chứng chế độ 6 Quá trình sinh lý 3D từng bước có mũi tên nhỏ gọn (Hấp thu nước & Quang hợp C3)
                ctrl.BatDauQuaTrinh("hap_thu_nuoc", 1);
                yield return WaitSeconds(1.2f);
                yield return WaitPlayFrames(4);
                RenderStageWithOverlay(ctrl.StageCamera, ctrl.UiCanvas, rt, tex, Path.Combine(outDir, "g3_7_qua_trinh_mui_ten_re.png"));

                ctrl.BatDauQuaTrinh("quang_hop_c3", 2);
                yield return WaitSeconds(1.2f);
                yield return WaitPlayFrames(4);
                RenderStageWithOverlay(ctrl.StageCamera, ctrl.UiCanvas, rt, tex, Path.Combine(outDir, "g3_8_qua_trinh_mui_ten_la.png"));
                ctrl.DongQuaTrinh();
                yield return WaitPlayFrames(4);

                // 7. Đo nhịp khung ở mức Cao khi mở mô-đun
                int sampleFrames = 45;
                float tStart = Time.realtimeSinceStartup;
                yield return WaitPlayFrames(sampleFrames);
                float elapsed = Mathf.Max(0.001f, Time.realtimeSinceStartup - tStart);
                float editorFps = sampleFrames / elapsed;
                Debug.Log($"[N18_G3_PERF] Cao mode PlayMode {sampleFrames} frames in {elapsed:F3}s (~{editorFps:F1} FPS in Editor)");
                Check(editorFps >= 20f, "G3 Stage runs smoothly at HardwareProfile Cao", $"editorFps={editorFps:F1}");

                // 8. Đóng mô-đun
                ThucVatMoDunImpl.Dong();
                yield return WaitPlayFrames(4);
                Check(!ThucVatMoDunImpl.DangMo, "G3 ThucVatMoDunImpl.Dong() closes cleanly");
            }
            finally
            {
                if (ThucVatMoDunImpl.DangMo) ThucVatMoDunImpl.Dong();
                HardwareProfile.DatTuDong();
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
                Application.logMessageReceived -= OnUnityLog;
            }

            FinishVerify(exitWhenDone, "G3");
        }

        // =====================================================================
        // Nhóm G4 — Giải phẫu người 3D (19/11: bản web BIO-LAB 3D cũ của CHEMISTRY, thay mô-đun C# G4)
        // =====================================================================

        static IEnumerator CoRunG4Verify(bool exitWhenDone)
        {
            Application.logMessageReceived -= OnUnityLog;
            Application.logMessageReceived += OnUnityLog;
            yield return WaitSceneReady();

            // 1. Bản web đầy đủ trong StreamingAssets (chạy ngoại tuyến, có nút về Lab)
            string goc = Path.Combine(Application.streamingAssetsPath, "biology_lab");
            string html = Path.Combine(Application.streamingAssetsPath, BioWebViewBridge.HtmlRelPath);
            string noiDung = File.Exists(html) ? File.ReadAllText(html) : "";
            string[] tepCan = { "sinh-hoc/bp3d.bin.gz", "sinh-hoc/giai-thich/index.js", "vendor/three/three.module.js",
                                "vendor/gsap.3.12.5.min.js", "vendor/mathjax.3.2.2.js", "dieu-khien-tay/cu-chi.js" };
            var thieu = tepCan.Where(t => !File.Exists(Path.Combine(goc, t))).ToList();
            Check(noiDung.Length > 300000 && thieu.Count == 0, "G4 Bản web BIO-LAB 3D đủ tệp trong StreamingAssets/biology_lab", "thiếu: " + string.Join(", ", thieu));
            Check(noiDung.Contains("id=\"nut-ve-lab\"") && noiDung.Contains("messageHandlers.unity") && noiDung.Contains("veralab://close")
                  && noiDung.Contains("./vendor/three/three.module.js") && !noiDung.Contains("cdn.jsdelivr.net/npm/three"),
                  "G4 Trang có nút Về Lab, gửi 'close' cho app, three.js nạp từ vendor (không cần mạng)");

            // 2. Plugin WKWebView cho Mac: bundle có mã máy, chỉ bật cho Standalone OSX
            string bundle = "Assets/Plugins/macOS/BioWebViewMac.bundle";
            bool coMa = File.Exists(Path.Combine(Application.dataPath, "Plugins/macOS/BioWebViewMac.bundle/Contents/MacOS/BioWebViewMac"));
            var imp = UnityEditor.AssetImporter.GetAtPath(bundle) as UnityEditor.PluginImporter;
            Check(coMa && imp != null && imp.GetCompatibleWithPlatform(UnityEditor.BuildTarget.StandaloneOSX) && !imp.GetCompatibleWithEditor(),
                  "G4 Plugins/macOS/BioWebViewMac.bundle có mã máy, bật cho bản Mac", $"coMa={coMa} imp={(imp != null)}");

            // 3. Cầu nối hợp đồng Khkt.Bio.GiaiPhau.GiaiPhauMoDun (thẻ Giải phẫu) → BioWebViewBridge
            System.Reflection.MethodInfo mMo = null, mDong = null;
            System.Reflection.PropertyInfo pDangMo = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var tBridge = asm.GetType("Khkt.Bio.GiaiPhau.GiaiPhauMoDun", false);
                if (tBridge == null) continue;
                mMo = tBridge.GetMethod("Mo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                mDong = tBridge.GetMethod("Dong", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                pDangMo = tBridge.GetProperty("DangMo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                break;
            }
            Check(mMo != null && mDong != null && pDangMo != null, "G4 Bridge contract Khkt.Bio.GiaiPhau.GiaiPhauMoDun found");

            int ucGoc = DesktopViewer.UiCapture;
            try
            {
                mMo?.Invoke(null, null);
                yield return WaitPlayFrames(2);
                bool mo = pDangMo != null && (bool)pDangMo.GetValue(null);
                Check(mo && BioWebViewBridge.IsOpen && !GiaiPhauMoDunImpl.DangMo && DesktopViewer.UiCapture == ucGoc + 1,
                      "G4 GiaiPhauMoDun.Mo() mở bản web (không mở mô-đun C# G4), khoá điều khiển lab",
                      $"cach={BioWebViewBridge.CachHien} uiCapture={DesktopViewer.UiCapture}");
                mDong?.Invoke(null, null);
                yield return WaitPlayFrames(2);
                Check(!BioWebViewBridge.IsOpen && DesktopViewer.UiCapture == ucGoc, "G4 GiaiPhauMoDun.Dong() đóng trang, trả điều khiển lab");

                // 4. Mở / đóng 5 lần không rò UiCapture
                bool gonHet = true;
                for (int c = 0; c < 5; c++)
                {
                    BioWebViewBridge.Open();
                    yield return WaitPlayFrames(1);
                    gonHet &= BioWebViewBridge.IsOpen;
                    BioWebViewBridge.Close();
                    yield return WaitPlayFrames(1);
                    gonHet &= !BioWebViewBridge.IsOpen;
                }
                Check(gonHet && DesktopViewer.UiCapture == ucGoc, "G4 Mở/đóng bản web 5 lần sạch sẽ", $"uiCapture={DesktopViewer.UiCapture}");

                // 5. Bàn giải phẫu số trong lab mở cùng bản web
                var table = UnityEngine.Object.FindAnyObjectByType<DigitalAnatomyTable>(FindObjectsInactive.Include);
                if (table != null)
                {
                    table.Use();
                    yield return WaitPlayFrames(2);
                    Check(BioWebViewBridge.IsOpen && !GiaiPhauMoDunImpl.DangMo, "G4 DigitalAnatomyTable.Use() mở bản web BIO-LAB 3D");
                    BioWebViewBridge.Close();
                    yield return WaitPlayFrames(1);
                }

                // 6. Máy chủ nội bộ cho Windows / Editor: phục vụ trang và module JS đúng kiểu
                bool bat = MayChuWebNoiBo.Bat(goc);
                long maHtml = 0, maJs = 0; string kieuJs = "";
                if (bat)
                {
                    using (var r = UnityEngine.Networking.UnityWebRequest.Get(MayChuWebNoiBo.Url("biology-lab-3d.html")))
                    {
                        r.SendWebRequest();
                        float han = Time.realtimeSinceStartup + 10f;
                        while (!r.isDone && Time.realtimeSinceStartup < han) yield return null;
                        maHtml = r.responseCode;
                        if (maHtml != 200) Debug.Log("[G4] html lỗi: " + r.error);
                    }
                    using (var r = UnityEngine.Networking.UnityWebRequest.Get(MayChuWebNoiBo.Url("vendor/three/three.module.js")))
                    {
                        r.SendWebRequest();
                        float han = Time.realtimeSinceStartup + 10f;
                        while (!r.isDone && Time.realtimeSinceStartup < han) yield return null;
                        maJs = r.responseCode;
                        kieuJs = r.GetResponseHeader("Content-Type") ?? "";
                    }
                }
                Check(bat && maHtml == 200 && maJs == 200 && kieuJs.StartsWith("text/javascript"),
                      "G4 Máy chủ nội bộ 127.0.0.1 phục vụ trang và module three.js", $"html={maHtml} js={maJs} {kieuJs}");
                MayChuWebNoiBo.Tat();
            }
            finally
            {
                if (BioWebViewBridge.IsOpen) BioWebViewBridge.Close();
                Application.logMessageReceived -= OnUnityLog;
            }

            FinishVerify(exitWhenDone, "G4");
        }

        static void RenderStageWithOverlay(Camera stageCam, Canvas overlayCanvas, RenderTexture rt, Texture2D tex, string path)
        {
            if (stageCam == null) return;
            RenderMode prevMode = RenderMode.ScreenSpaceOverlay;
            Camera prevWorldCam = null;
            float prevPlaneDist = 100f;
            int prevLayer = 5;
            var oldRt = stageCam.targetTexture;
            bool oldEn = stageCam.enabled;
            int oldMask = stageCam.cullingMask;
            float oldNear = stageCam.nearClipPlane;
            try
            {
                stageCam.enabled = true;
                stageCam.cullingMask = oldMask | (1 << 5);
                stageCam.nearClipPlane = 0.02f;
                stageCam.targetTexture = rt;
                if (overlayCanvas != null)
                {
                    prevMode = overlayCanvas.renderMode;
                    prevWorldCam = overlayCanvas.worldCamera;
                    prevPlaneDist = overlayCanvas.planeDistance;
                    prevLayer = overlayCanvas.gameObject.layer;
                    overlayCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    overlayCanvas.worldCamera = stageCam;
                    overlayCanvas.planeDistance = 0.25f;
                    Canvas.ForceUpdateCanvases();
                }
                stageCam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                stageCam.targetTexture = oldRt;
                stageCam.enabled = oldEn;
                stageCam.cullingMask = oldMask;
                stageCam.nearClipPlane = oldNear;
                if (overlayCanvas != null)
                {
                    overlayCanvas.renderMode = prevMode;
                    overlayCanvas.worldCamera = prevWorldCam;
                    overlayCanvas.planeDistance = prevPlaneDist;
                    overlayCanvas.gameObject.layer = prevLayer;
                    Canvas.ForceUpdateCanvases();
                }
            }
        }

        static void CheckConnectedWrist(ArmRig armRig, HandRig rig, string checkName)
        {
            if (armRig == null || rig == null || rig.WristAttach == null)
            {
                Check(false, checkName, "Missing ArmRig or WristAttach");
                return;
            }
            float gapMm = Vector3.Distance(armRig.RightWrist, rig.WristAttach.position) * 1000f;
            Check(gapMm <= 5.0f, checkName, $"gap={gapMm:0.00}mm");
        }

        static Vector3[] BuildSyntheticPinchLandmarks()
        {
            var lm = new Vector3[21];
            lm[0] = Vector3.zero;
            lm[1] = new Vector3(-0.025f, -0.005f, 0.020f);
            lm[2] = new Vector3(-0.030f, -0.015f, 0.042f);
            lm[3] = new Vector3(-0.020f, -0.024f, 0.062f);
            lm[4] = new Vector3(-0.010f, -0.028f, 0.075f);

            int[] bases = { 5, 9, 13, 17 };
            for (int i = 0; i < 4; i++)
            {
                int b = bases[i];
                float x = (i - 1.5f) * 0.018f;
                lm[b] = new Vector3(x, 0f, 0.085f);
                lm[b + 1] = lm[b] + new Vector3(0f, -0.022f, 0.025f);
                lm[b + 2] = lm[b + 1] + new Vector3(0f, -0.020f, 0.005f);
                lm[b + 3] = lm[b + 2] + new Vector3(0f, -0.014f, -0.006f);
            }
            return lm;
        }

        static void FinishVerify(bool exitWhenDone, string tag = "G2")
        {
            Debug.Log($"[N18_{tag}_SUMMARY] passes={passes} failures={failures} runtimeErrors={runtimeErrors}");
            if (failedNotes.Count > 0)
                Debug.LogError("[N18_FAILED_LIST] " + string.Join(" || ", failedNotes));
            if (runtimeErrNotes.Count > 0)
                Debug.LogError("[N18_RUNTIME_ERR_LIST] " + string.Join(" || ", runtimeErrNotes));

            bool ok = failures == 0 && runtimeErrors == 0;
            Debug.Log(ok ? $"[N18_{tag}_RESULT] ALL PASSED" : $"[N18_{tag}_RESULT] FAILED");
            if (exitWhenDone) EditorApplication.Exit(ok ? 0 : 1);
        }

        static Camera CreateAuxCamera(GameObject go, int cullingMask)
        {
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.015f;
            cam.farClipPlane = 60f;
            cam.cullingMask = cullingMask;
            return cam;
        }

        static void PositionInFrontOfStopcock(DesktopViewer viewer, HandController hand, Vector3 basePos, BuretStopcock stopcock)
        {
            Vector3 scPos = stopcock.transform.position;
            Vector3 dirToSc = new Vector3(scPos.x - basePos.x, 0f, scPos.z - basePos.z).normalized;
            if (dirToSc.sqrMagnitude < 1e-4f) dirToSc = Vector3.forward;
            Vector3 camStand = new Vector3(scPos.x - dirToSc.x * 0.48f, 0f, scPos.z - dirToSc.z * 0.48f);
            float yaw = Mathf.Atan2(dirToSc.x, dirToSc.z) * Mathf.Rad2Deg;
            ResetViewer(viewer, hand, camStand, yaw, 22f);
            Physics.SyncTransforms();
            hand.OverrideAimPoint = scPos;
        }

        static void ResetViewer(DesktopViewer viewer, HandController hand, Vector3 pos, float yaw, float pitch)
        {
            if (hand != null)
            {
                if (hand.Held != null) hand.ReturnHome();
                hand.OverrideAimPoint = null;
            }
            viewer.SetPose(pos, yaw, pitch);
            Physics.SyncTransforms();
        }

        static Vector3 AimInFront(Camera cam, float dist, float upOffset, float rightOffset)
        {
            return cam.transform.position + cam.transform.forward * dist + cam.transform.up * upOffset + cam.transform.right * rightOffset;
        }

        static void ForceBaselineCurl(HandRig rig, float curlVal, Renderer[] renderers)
        {
            if (rig == null) return;
            rig.curl = curlVal;
            var t = typeof(HandRig);
            var fiShown = t.GetField("shownCurl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fiShown?.SetValue(rig, curlVal);
            var miUpdate = t.GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            miUpdate?.Invoke(rig, null);
            ForceRenderersVisible(renderers, true);
        }

        static void ForceRenderersVisible(Renderer[] renderers, bool on)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = on;
        }

        static Grabbable FindGrabbableByKind(string kind)
        {
            var all = UnityEngine.Object.FindObjectsByType<Grabbable>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                var g = all[i];
                if (g != null && string.Equals(g.kind, kind, StringComparison.OrdinalIgnoreCase))
                    return g;
            }
            return null;
        }

        static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == targetName) return c;
                var nested = FindChildRecursive(c, targetName);
                if (nested != null) return nested;
            }
            return null;
        }

        static void CapturePair(Camera mainCam, Camera sideCam, RenderTexture rt, Texture2D tex, string outDir, string prefix, Transform handRoot, Renderer[] renderers)
        {
            ForceRenderersVisible(renderers, true);
            RenderToFile(mainCam, rt, tex, Path.Combine(outDir, prefix + "_goc_nguoi_choi.png"), includeUi: true);

            Vector3 pivot = handRoot.position + handRoot.forward * 0.03f;
            Vector3 eye = pivot + mainCam.transform.right * 0.25f + mainCam.transform.forward * 0.14f + Vector3.up * 0.07f;
            sideCam.transform.position = eye;
            sideCam.transform.LookAt(pivot - mainCam.transform.forward * 0.03f, Vector3.up);
            RenderToFile(sideCam, rt, tex, Path.Combine(outDir, prefix + "_goc_nghieng.png"), includeUi: false);
        }

        static void RenderToFile(Camera cam, RenderTexture rt, Texture2D tex, string path, bool includeUi)
        {
            Canvas.ForceUpdateCanvases();
            var old = cam.targetTexture;
            bool oldEnabled = cam.enabled;
            int oldMask = cam.cullingMask;
            float oldNear = cam.nearClipPlane;
            Camera overlayCam = null;
            bool overlayOldEnabled = false;
            var swapped = new List<(Canvas c, Camera cam, float dist)>();

            if (includeUi)
            {
                var ui = UnityEngine.Object.FindAnyObjectByType<ModernUI>();
                var ovTr = cam.transform.Find("UI Overlay Camera");
                if (ovTr != null) overlayCam = ovTr.GetComponent<Camera>();
                if (overlayCam != null)
                {
                    overlayOldEnabled = overlayCam.enabled;
                    overlayCam.enabled = false;
                }
                cam.cullingMask |= (1 << 5);
                float shotDist = Mathf.Max(0.02f, Mathf.Min(oldNear, 0.04f));
                cam.nearClipPlane = 0.015f;
                var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
                for (int i = 0; i < allCanvases.Length; i++)
                {
                    var c = allCanvases[i];
                    if (c == null || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceCamera) continue;
                    if (c.worldCamera == overlayCam || c.worldCamera == cam || (ui != null && c == ui.Canvas))
                    {
                        swapped.Add((c, c.worldCamera, c.planeDistance));
                        c.worldCamera = cam;
                        c.planeDistance = shotDist;
                    }
                }
                Canvas.ForceUpdateCanvases();
            }

            try
            {
                cam.enabled = true;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = old;
                cam.enabled = oldEnabled;
                cam.cullingMask = oldMask;
                cam.nearClipPlane = oldNear;
                for (int i = 0; i < swapped.Count; i++)
                {
                    var s = swapped[i];
                    if (s.c == null) continue;
                    s.c.worldCamera = s.cam;
                    s.c.planeDistance = s.dist;
                }
                if (overlayCam != null) overlayCam.enabled = overlayOldEnabled;
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
