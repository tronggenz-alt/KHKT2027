using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Khkt.Input
{
    /// <summary>
    /// Chạy tracker bàn tay Python riêng (tools/tay_tracker.py) khi vào lab, giống cách chạy ngoài của hand_rects.py:
    /// camera ở luồng riêng, chỉ HandLandmarker, ảnh 320 px. Unity chỉ nhận gói UDP nên game không bị giật.
    ///  • Python không chạy được (thiếu Python / MediaPipe, bị chặn) thì bật InAppHandTracker làm dự phòng.
    ///  • F8 (điều khiển bằng đầu) cũng dùng webcam: lúc đó tạm dừng tracker này, tracker của F8 vẫn gửi tay.
    ///  • F10 bật / tắt nhận diện tay.
    /// </summary>
    public class HandTrackerProcess : MonoBehaviour
    {
        public static HandTrackerProcess Instance { get; private set; }
        public bool Enabled { get; private set; } = true;
        public bool UsingFallback { get; private set; }
        /// <summary>Đang dùng MediaPipe C# ngay trong game (không có tiến trình Python).</summary>
        public bool UsingPlugin { get; private set; }
        public string LastError { get; private set; } = "";

        Process proc;
        System.Threading.Tasks.Task<string> finding;   // dò Python ở luồng phụ: thử import mediapipe mất 1–2 giây
        string script;
        float launchedAt = -1f;
        int packetsAtLaunch;
        bool pausedForHead, announced, skipPlugin;

        void Awake() => Instance = this;

        System.Collections.IEnumerator Start()
        {
            if (Application.isBatchMode) yield break;
            yield return new WaitForSecondsRealtime(0.8f);
            if (Enabled) Begin();
        }

        void OnDestroy()
        {
            Kill();
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit() => Kill();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame)
            {
                Enabled = !Enabled;
                if (Enabled) Begin();
                else
                {
                    Kill();
                    if (InAppHandTracker.Instance != null) InAppHandTracker.Instance.Stop();
#if KHKT_MEDIAPIPE_PLUGIN
                    if (PluginHandTracker.Instance != null) PluginHandTracker.Instance.Stop();
#endif
                    UsingPlugin = false;
                    UsingFallback = false;
                    LabMessages.Say("Nhận diện tay: TẮT (F10 để bật)", LabMessages.Tone.Info);
                }
            }
#if KHKT_MEDIAPIPE_PLUGIN
            // MediaPipe C# nạp hỏng (ở luồng phụ, báo về sau): chuyển sang tracker Python
            if (UsingPlugin && PluginHandTracker.Instance != null && PluginHandTracker.Instance.Failed)
            {
                Debug.LogWarning("[TayC#] Không chạy được, chuyển sang tracker Python: " + PluginHandTracker.Instance.LastError);
                UsingPlugin = false;
                skipPlugin = true;
                Begin();
                return;
            }
#endif
            if (!Enabled || UsingFallback || UsingPlugin) return;
            if (finding != null)
            {
                if (!finding.IsCompleted) return;
                string py = finding.IsFaulted ? null : finding.Result;
                finding = null;
                if (!Launch(py)) Fallback();
                return;
            }

            // F8 đang giữ webcam: nhường camera
            bool head = HeadTracking.Instance != null && HeadTracking.Instance.ProcessRunning;
            if (head && !pausedForHead) { pausedForHead = true; Kill(); return; }
            if (!head && pausedForHead) { pausedForHead = false; Begin(); return; }
            if (pausedForHead) return;

            var at = ArmTracking.Instance;
            if (proc != null && proc.HasExited)
            {
                Debug.LogWarning("[TayPy] Tracker dừng: " + LastError);
                proc = null;
                Fallback();
                return;
            }
            if (proc != null && at != null && !announced && at.Packets > packetsAtLaunch)
            {
                announced = true;
                LabMessages.Say("Nhận diện tay: BẬT. Đưa tay vào khung webcam (F10 tắt)", LabMessages.Tone.Good);
            }
            // chạy 15 giây mà chưa có gói nào (nạp mô hình lâu nhất khoảng 3 giây): coi như hỏng
            if (proc != null && !announced && Time.realtimeSinceStartup - launchedAt > 15f)
            {
                Kill();
                Fallback();
            }
        }

        void Begin()
        {
            UsingFallback = false;
            UsingPlugin = false;
            announced = false;
            if (HeadTracking.Instance != null && HeadTracking.Instance.ProcessRunning) { pausedForHead = true; return; }
            if (proc != null || finding != null) return;
#if KHKT_MEDIAPIPE_PLUGIN
            // có MediaPipe C# trong game: dùng luôn, không cần Python
            var plug = PluginHandTracker.Instance != null ? PluginHandTracker.Instance : gameObject.AddComponent<PluginHandTracker>();
            if (!skipPlugin && plug.Begin())
            {
                UsingPlugin = true;
                LabMessages.Say("Nhận diện tay: BẬT (MediaPipe C#). Đưa tay vào khung webcam (F10 tắt)", LabMessages.Tone.Good);
                return;
            }
            Debug.LogWarning("[TayC#] Không chạy được, chuyển sang tracker Python: " + plug.LastError);
#endif
            script = FindScript();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            finding = System.Threading.Tasks.Task.Run(() => FindPython(root));
        }

        void Fallback()
        {
            UsingFallback = true;
            if (InAppHandTracker.Instance != null)
            {
                LabMessages.Say("Không chạy được tracker Python, dùng nhận diện tay trong ứng dụng (chậm hơn)", LabMessages.Tone.Warn);
                InAppHandTracker.Instance.Begin();
            }
        }

        bool Launch(string py)
        {
            if (py == null || script == null)
            {
                LastError = py == null ? "không tìm thấy Python" : "không tìm thấy tools/tay_tracker.py";
                Debug.LogWarning("[TayPy] " + LastError);
                return false;
            }
            int port = (HeadTracking.Instance != null ? HeadTracking.Instance.port : 9051) + 3;
            string cam = HeadTracking.Instance != null ? HeadTracking.Instance.cameraIndex : "may";
            string args = $"-X utf8 \"{script}\" --cong {port} --cam {cam} --cha {Process.GetCurrentProcess().Id}";
            try
            {
                var psi = new ProcessStartInfo(py, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8,
                    WorkingDirectory = Path.GetDirectoryName(script),
                };
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                proc = Process.Start(psi);
                if (proc == null) return false;
                proc.ErrorDataReceived += (_, a) =>
                {
                    if (!string.IsNullOrWhiteSpace(a.Data) && !a.Data.StartsWith("W0000") && !a.Data.StartsWith("I0000")) LastError = a.Data.Trim();
                };
                proc.OutputDataReceived += (_, a) => { if (!string.IsNullOrWhiteSpace(a.Data)) Debug.Log("[TayPy] " + a.Data); };
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();
                launchedAt = Time.realtimeSinceStartup;
                packetsAtLaunch = ArmTracking.Instance != null ? ArmTracking.Instance.Packets : 0;
                Debug.Log($"[TayPy] Đã chạy {py} {args}");
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                proc = null;
                return false;
            }
        }

        void Kill()
        {
            if (proc == null) return;
            try { if (!proc.HasExited) proc.Kill(); } catch { }
            try { proc.Dispose(); } catch { }
            proc = null;
        }

        static string FindScript()
        {
            foreach (var p in new[]
            {
                Path.Combine(Application.dataPath, "..", "..", "tools", "tay_tracker.py"),
                Path.Combine(Application.dataPath, "..", "tools", "tay_tracker.py"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "tay_tracker.py"),
            })
                if (File.Exists(p)) return Path.GetFullPath(p);
            return null;
        }

        static string FindPython(string root)
        {
            var env = System.Environment.GetEnvironmentVariable("KHKT_PYTHON");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            foreach (var p in new[] { Path.Combine(root, ".venv", "Scripts", "python.exe"), Path.Combine(root, ".venv", "bin", "python") })
                if (File.Exists(p)) return p;
            foreach (var cmd in new[] { "python", "py", "python3" })
            {
                try
                {
                    using (var p = Process.Start(new ProcessStartInfo(cmd, "-c \"import mediapipe\"") { UseShellExecute = false, CreateNoWindow = true }))
                    {
                        if (p.WaitForExit(8000) && p.ExitCode == 0) return cmd;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
