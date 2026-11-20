using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Khkt.Avatar;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Input;
using Khkt.Interaction;
using Khkt.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Bộ kiểm thử + chụp ảnh tay và cánh tay người học (ngày 20/11/2026).
    ///  • ChupTruoc: chụp 8 tư thế × 2 góc vào docs/ngay-20-11/anh/tay-moi/truoc_*.png (chạy trên mã cũ).
    ///  • Run: kiểm thử tay mới + chụp sau_*.png cùng các tư thế, cùng góc.
    /// Chạy: Unity -batchmode -projectPath ... -executeMethod Khkt.EditorTools.KhktTayMoiVerify.Run (không dùng -nographics).
    /// </summary>
    [InitializeOnLoad]
    public static partial class KhktTayMoiVerify
    {
        const string PrefCmd = "VERA_TAY_MOI_CMD";
        const int ShotW = 1600, ShotH = 1000;

        static IEnumerator activeRoutine;
        static readonly Stack<IEnumerator> callStack = new Stack<IEnumerator>();
        static float deadlineRealtime;
        static int passes, failures, runtimeErrors;
        static readonly List<string> failedNotes = new List<string>();
        static readonly List<string> runtimeErrNotes = new List<string>();

        static KhktTayMoiVerify()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            TryStartPendingCommand();
        }

        public static void ChupTruoc() => Launch("TRUOC");
        public static void Run() => Launch("RUN");

        static void OnPlayModeStateChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode) TryStartPendingCommand();
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
            deadlineRealtime = Time.realtimeSinceStartup + 300f;
            passes = failures = runtimeErrors = 0;
            failedNotes.Clear();
            runtimeErrNotes.Clear();
            activeRoutine = cmd == "TRUOC" ? CoCapture("truoc", exitWhenDone: true) : CoRun();
        }

        static void Launch(string cmd)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[TAY] Đang ở PlayMode."); return; }
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PhongThiNghiem.unity");
            if (!scene.IsValid()) { Debug.LogError("[TAY_FATAL] Không mở được cảnh PhongThiNghiem"); EditorApplication.Exit(1); return; }
            foreach (var s in UnityEngine.Object.FindObjectsByType<SerialControllerSource>(FindObjectsInactive.Include))
                UnityEngine.Object.DestroyImmediate(s);
            EditorPrefs.SetString(PrefCmd, cmd);
            EditorApplication.EnterPlaymode();
        }

        static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (condition != null && (condition.StartsWith("[TAY") || condition.StartsWith("Curl error"))) return;
            runtimeErrors++;
            if (runtimeErrNotes.Count < 8) runtimeErrNotes.Add($"{condition} | {stackTrace}");
        }

        static void Check(bool cond, string name, string detail = "")
        {
            string msg = (cond ? "[TAY_PASS] " : "[TAY_FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : " | " + detail);
            if (cond) { passes++; Debug.Log(msg); }
            else { failures++; failedNotes.Add(msg); Debug.LogError(msg); }
        }

        static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying) return;
            if (Time.realtimeSinceStartup > deadlineRealtime)
            {
                EditorApplication.update -= OnEditorUpdate;
                Debug.LogError("[TAY_TIMEOUT] Hết giờ.");
                EditorApplication.Exit(2);
                return;
            }
            if (activeRoutine == null) { EditorApplication.update -= OnEditorUpdate; return; }
            try
            {
                var top = callStack.Count > 0 ? callStack.Peek() : activeRoutine;
                if (top.MoveNext())
                {
                    if (top.Current is IEnumerator nested) callStack.Push(nested);
                }
                else if (callStack.Count > 0) callStack.Pop();
                else { activeRoutine = null; EditorApplication.update -= OnEditorUpdate; }
            }
            catch (Exception e)
            {
                activeRoutine = null;
                callStack.Clear();
                EditorApplication.update -= OnEditorUpdate;
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static IEnumerator WaitFrames(int n)
        {
            int target = Time.frameCount + Mathf.Max(1, n);
            while (Time.frameCount < target) yield return null;
        }

        static IEnumerator WaitSeconds(float s)
        {
            float until = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        static IEnumerator WaitSceneReady()
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 25f)
            {
                if (UnityEngine.Object.FindAnyObjectByType<DesktopViewer>() != null &&
                    UnityEngine.Object.FindAnyObjectByType<HandController>() != null &&
                    UnityEngine.Object.FindAnyObjectByType<ModernUI>() != null) break;
                yield return null;
            }
            yield return WaitFrames(15);
            var intro = UnityEngine.Object.FindAnyObjectByType<IntroScreen>();
            if (intro != null && intro.IsOpen) intro.Close();
            var viewer = UnityEngine.Object.FindAnyObjectByType<DesktopViewer>();
            if (viewer != null) viewer.GoTo(2);
            yield return WaitFrames(4);
            float ts = Time.realtimeSinceStartup;
            while (!ZoneStreamer.Settled && Time.realtimeSinceStartup - ts < 12f) yield return null;
            yield return WaitSeconds(0.4f);
            yield return WaitFrames(10);
        }

        static string ShotDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "ngay-20-11", "anh", "tay-moi"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ------------------------------------------------------------------
        // Chụp 8 tư thế × 2 góc (người chơi + nghiêng cận cảnh)
        // ------------------------------------------------------------------

        struct PoseShot
        {
            public string name;
            public HandPoseId pose;
            public string pickKind;   // nếu khác null: cầm dụng cụ loại này
            public bool stopcock;
        }

        static readonly PoseShot[] Shots =
        {
            new PoseShot { name = "1_xoe", pose = HandPoseId.Xoe },
            new PoseShot { name = "2_nghi", pose = HandPoseId.Nghi },
            new PoseShot { name = "3_chum_cam", pose = HandPoseId.ChumCam },
            new PoseShot { name = "4_binh_non", pose = HandPoseId.BinhNon, pickKind = "binh_non" },
            new PoseShot { name = "5_ong_nghiem", pose = HandPoseId.OngNghiem, pickKind = "ong_nghiem" },
            new PoseShot { name = "6_pipet", pose = HandPoseId.Pipet, pickKind = "pipet" },
            new PoseShot { name = "7_khoa_buret", pose = HandPoseId.KhoaBuret, stopcock = true },
            new PoseShot { name = "8_chi_tay", pose = HandPoseId.ChiTay },
        };

        /// <summary>Kết quả IK từng tư thế (để so trước/sau và để kiểm thử).</summary>
        static readonly Dictionary<string, (int pen, float errMm)> IkResult = new Dictionary<string, (int, float)>();

        static IEnumerator CoCapture(string prefix, bool exitWhenDone)
        {
            if (exitWhenDone) yield return WaitSceneReady();

            var viewer = UnityEngine.Object.FindAnyObjectByType<DesktopViewer>();
            var hand = HandController.Active != null ? HandController.Active : UnityEngine.Object.FindAnyObjectByType<HandController>();
            Camera mainCam = hand != null && hand.viewCamera != null ? hand.viewCamera : Camera.main;
            if (viewer == null || hand == null || hand.hand == null || mainCam == null)
            {
                Check(false, "Cảnh có DesktopViewer / HandController / HandRig / Camera");
                if (exitWhenDone) EditorApplication.Exit(1);
                yield break;
            }

            HandRig rig = hand.hand;
            HandRig.WearGloves = true;
            string outDir = ShotDir();
            var sideGo = new GameObject("TAY_SideCam");
            var sideCam = sideGo.AddComponent<Camera>();
            sideCam.enabled = false;
            sideCam.fieldOfView = 40f;
            sideCam.nearClipPlane = 0.01f;
            sideCam.farClipPlane = 60f;
            sideCam.cullingMask = mainCam.cullingMask;
            var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);

            Vector3 basePos = viewer.transform.position;
            float baseYaw = viewer.transform.eulerAngles.y;
            IkResult.Clear();

            try
            {
                foreach (var s in Shots)
                {
                    viewer.SetPose(basePos, baseYaw, 18f);
                    if (hand.Held != null) hand.ReturnHome();
                    hand.OverrideAimPoint = null;
                    rig.PoseOverride = null;
                    rig.TargetContactObject = null;
                    Physics.SyncTransforms();
                    yield return WaitFrames(3);

                    if (s.stopcock)
                    {
                        var sc = UnityEngine.Object.FindAnyObjectByType<BuretStopcock>();
                        if (sc == null) { Check(false, "Tìm thấy khóa buret"); continue; }
                        Vector3 scPos = sc.transform.position;
                        Vector3 dir = new Vector3(scPos.x - basePos.x, 0f, scPos.z - basePos.z).normalized;
                        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
                        viewer.SetPose(new Vector3(scPos.x - dir.x * 0.48f, 0f, scPos.z - dir.z * 0.48f), Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 22f);
                        Physics.SyncTransforms();
                        hand.OverrideAimPoint = scPos;
                        rig.PoseOverride = s.pose;
                        rig.TargetContactObject = sc.transform;
                    }
                    else if (s.pickKind != null)
                    {
                        var g = FindGrabbable(s.pickKind);
                        if (g == null) { Check(false, "Tìm thấy dụng cụ " + s.pickKind); continue; }
                        hand.Pick(g);
                        hand.OverrideAimPoint = Aim(mainCam, 0.44f, -0.08f, 0.05f);
                        rig.PoseOverride = s.pose;
                    }
                    else
                    {
                        hand.OverrideAimPoint = Aim(mainCam, 0.42f, -0.06f, 0.06f);
                        rig.PoseOverride = s.pose;
                    }

                    yield return WaitSeconds(0.45f);
                    yield return WaitFrames(6);
                    if (s.stopcock || s.pickKind != null)
                        IkResult[s.name] = (rig.LastPenetrationCount, rig.LastMaxSurfaceErrorMm);
                    Debug.Log($"[TAY_SO] {prefix}_{s.name} pose={rig.CurrentPose} pen={rig.LastPenetrationCount} err={rig.LastMaxSurfaceErrorMm:0.00}mm");
                    RecordArm(s.name, rig);

                    RenderToFile(mainCam, rt, tex, Path.Combine(outDir, $"{prefix}_{s.name}_goc_nguoi_choi.png"));
                    Vector3 pivot = rig.transform.position + rig.transform.forward * 0.02f;
                    sideCam.transform.position = pivot + mainCam.transform.right * 0.24f + mainCam.transform.forward * 0.10f + Vector3.up * 0.08f;
                    sideCam.transform.LookAt(pivot - mainCam.transform.forward * 0.05f, Vector3.up);
                    RenderToFile(sideCam, rt, tex, Path.Combine(outDir, $"{prefix}_{s.name}_goc_nghieng.png"));
                }

                // Ảnh da trần (tắt găng) cận cảnh, tư thế nghỉ
                if (hand.Held != null) hand.ReturnHome();
                viewer.SetPose(basePos, baseYaw, 18f);
                hand.OverrideAimPoint = Aim(mainCam, 0.42f, -0.06f, 0.06f);
                rig.PoseOverride = HandPoseId.Nghi;
                HandRig.WearGloves = false;
                yield return WaitSeconds(0.4f);
                yield return WaitFrames(4);
                Vector3 pv = rig.transform.position + rig.transform.forward * 0.02f;
                sideCam.transform.position = pv + mainCam.transform.right * 0.20f + mainCam.transform.forward * 0.12f + Vector3.up * 0.10f;
                sideCam.transform.LookAt(pv - mainCam.transform.forward * 0.04f, Vector3.up);
                RenderToFile(sideCam, rt, tex, Path.Combine(outDir, $"{prefix}_9_da_tran_can_canh.png"));
                RenderToFile(mainCam, rt, tex, Path.Combine(outDir, $"{prefix}_9_da_tran_goc_nguoi_choi.png"));

                // Toàn cảnh hai cánh tay (lùi camera, nhìn chéo từ trên)
                HandRig.WearGloves = true;
                rig.PoseOverride = HandPoseId.Nghi;
                yield return WaitFrames(4);
                Vector3 c = mainCam.transform.position;
                sideCam.fieldOfView = 55f;
                sideCam.transform.position = c + mainCam.transform.right * 0.55f + Vector3.up * 0.25f + mainCam.transform.forward * 0.30f;
                sideCam.transform.LookAt(c + mainCam.transform.forward * 0.25f - Vector3.up * 0.25f, Vector3.up);
                RenderToFile(sideCam, rt, tex, Path.Combine(outDir, $"{prefix}_10_hai_canh_tay.png"));
                Debug.Log($"[TAY_CHUP] Xong ảnh {prefix}_* vào {outDir}");
            }
            finally
            {
                HandRig.WearGloves = true;
                rig.PoseOverride = null;
                rig.TargetContactObject = null;
                hand.OverrideAimPoint = null;
                if (hand.Held != null) hand.ReturnHome();
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(sideGo);
            }

            if (exitWhenDone) EditorApplication.Exit(0);
        }

        static Grabbable FindGrabbable(string kind)
        {
            var all = UnityEngine.Object.FindObjectsByType<Grabbable>(FindObjectsInactive.Exclude);
            foreach (var g in all)
                if (g != null && string.Equals(g.kind, kind, StringComparison.OrdinalIgnoreCase)) return g;
            return null;
        }

        static Vector3 Aim(Camera cam, float dist, float up, float right) =>
            cam.transform.position + cam.transform.forward * dist + cam.transform.up * up + cam.transform.right * right;

        static void RenderToFile(Camera cam, RenderTexture rt, Texture2D tex, string path)
        {
            var old = cam.targetTexture;
            bool oldEnabled = cam.enabled;
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
            }
        }

        static readonly List<string> ArmNotes = new List<string>();
        static float armMaxGapMm, armMaxFlex, armMinFlex = 999f, armMaxPron = -999f, armMinPron = 999f, armMaxDev;

        static void RecordArm(string name, HandRig rig)
        {
            var arm = UnityEngine.Object.FindAnyObjectByType<ArmRig>();
            if (arm == null || rig.WristAttach == null) return;
            float gap = Vector3.Distance(arm.RightWrist, rig.WristAttach.position) * 1000f;
            float flex = arm.ElbowFlexDeg(0), pron = arm.PronationDeg(0), dev = arm.WristDeviationDeg(0);
            armMaxGapMm = Mathf.Max(armMaxGapMm, gap);
            armMaxFlex = Mathf.Max(armMaxFlex, flex); armMinFlex = Mathf.Min(armMinFlex, flex);
            armMaxPron = Mathf.Max(armMaxPron, pron); armMinPron = Mathf.Min(armMinPron, pron);
            armMaxDev = Mathf.Max(armMaxDev, dev);
            string note = $"{name}: hở={gap:0.00}mm khuỷu={flex:0}° sấp={pron:0}° cổ tay={dev:0}°";
            ArmNotes.Add(note);
            Debug.Log("[TAY_TAY] " + note);
        }

        static void FinishVerify(bool exit)
        {
            Debug.Log($"[TAY_SUMMARY] passes={passes} failures={failures} runtimeErrors={runtimeErrors}");
            if (failedNotes.Count > 0) Debug.LogError("[TAY_FAILED_LIST] " + string.Join(" || ", failedNotes));
            if (runtimeErrNotes.Count > 0) Debug.LogError("[TAY_RUNTIME_ERR_LIST] " + string.Join(" || ", runtimeErrNotes));
            bool ok = failures == 0 && runtimeErrors == 0;
            Debug.Log(ok ? "[TAY_RESULT] ALL PASSED" : "[TAY_RESULT] FAILED");
            if (exit) EditorApplication.Exit(ok ? 0 : 1);
        }

    }
}
