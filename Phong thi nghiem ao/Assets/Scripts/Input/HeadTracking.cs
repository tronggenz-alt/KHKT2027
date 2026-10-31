using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Khkt.Input
{
    public enum TrackingEngine
    {
        MediaPipe,
        YuNet
    }

    /// <summary>
    /// Điều khiển góc nhìn bằng ĐẦU, TRÒNG MẮT và khoảng cách qua webcam (MediaPipe / YuNet):
    ///  • F8 bật / tắt: Unity tự chạy Python ở chế độ ngầm và tắt khi thoát.
    ///  • F9 căn giữa: nhìn thẳng màn hình 2 giây để ghi nhớ tư thế giữa (yaw, pitch, roll, distance, gaze).
    ///  • Quay đầu quá ~30° thì góc nhìn xoay liên tục về phía đó; nhìn thẳng thì dừng.
    ///  • Ghé gần webcam thì phóng to (Zoom), ngả ra xa thì nhìn rộng ra.
    ///  • Mất mặt quá 0.4 s: thôi xoay, zoom từ từ về 1.0.
    ///  • Tự động lùi về YuNet (head_tracker.py) nếu MediaPipe gặp lỗi hoặc máy chưa cài gói mediapipe.
    /// </summary>
    public class HeadTracking : MonoBehaviour
    {
        public static HeadTracking Instance { get; private set; }

        public TrackingEngine engine = TrackingEngine.MediaPipe;
        public int port = 9051;
        [Tooltip("Chế độ hiệu năng MediaPipe: muot, can-bang, tiet-kiem")]
        public string cheDo = "muot";
        [Tooltip("Đường dẫn thực thi Python; để trống = tự động dò (.venv, python3, python, py)")]
        public string pythonPath = "";
        [Tooltip("Đường dẫn file script; để trống = tự tìm trong thư mục tools")]
        public string scriptPath = "";
        [Tooltip("Webcam: 'may' = tự chọn webcam máy tính (chặn điện thoại), hoặc số thứ tự")]
        public string cameraIndex = "may";

        [Header("Kiểu lái (Yaw)")]
        [Tooltip("Quay đầu dưới góc này thì không xoay (vùng chết 30°)")]
        public float deadZoneDeg = 30f;
        public float fullTurnDeg = 50f;
        public float smoothingRate = 12f;

        [Header("Cúi / ngẩng (Pitch)")]
        [Tooltip("Cho phép nhìn lên / xuống khi cúi / ngẩng đầu (tắt mặc định)")]
        public bool enablePitchLook = false;
        public float pitchDeadZoneDeg = 15f;
        public float fullPitchDeg = 35f;
        public float pitchTurnSpeed = 40f;

        [Header("Zoom & Khoảng cách")]
        [Tooltip("Lệch khoảng cách (tỉ lệ) chưa tính là ghé gần / ngả xa")]
        public float zoomDeadZone = 0.07f;
        public float maxZoom = 2.5f;
        public float minZoom = 0.8f;
        public float calibrateSeconds = 2f;

        // ---- Kết quả tĩnh dùng chung ----
        public static bool Active => Instance != null && Instance.running && Instance.calibrated;
        public static float Turn => Instance != null ? Instance.turn : 0f;
        public static float PitchTurn => Instance != null && Instance.enablePitchLook ? Instance.pitchTurn : 0f;
        public static float Zoom => Instance != null ? Instance.zoom : 1f;
        public static float Pitch => Instance != null ? Instance.pitchVal : 0f;
        public static float Roll => Instance != null ? Instance.rollVal : 0f;
        public static float DistanceCm => Instance != null ? Instance.distCmVal : 0f;
        public static Vector2 Gaze => Instance != null ? Instance.gazeVal : Vector2.zero;
        public static bool Blink => Instance != null && Instance.blinkVal;
        public static string Engine => Instance != null ? Instance.currentEngine.ToString() : "-";
        public static HandData[] LatestHands => Instance != null ? Instance.latestHands : null;

        public static event Action<HandData[], double> OnHandsReceived;
        public static event Action<Packet> OnPacketReceived;

        public string Status { get; private set; } = "Tắt";

        [Serializable]
        public class HandData
        {
            public string ben;
            public int ok;
            public float x;
            public float y;
            public float co;
            public int pinch;
            public string cu_chi;
            public float tin;
        }

        [Serializable]
        public class Packet
        {
            public int ok;
            public float yaw;
            public float pitch;
            public float e;
            public double t;
            public string nguon;
            public float roll;
            public float dist_cm;
            public float gaze_x;
            public float gaze_y;
            public int blink_l;
            public int blink_r;
            public float fps;
            public HandData[] hands;
        }

        Process proc;
        UdpClient udp;
        Thread thread;
        volatile bool listening;
        readonly object gate = new object();

        // Bộ đệm và tái sử dụng bộ nhớ (GC Alloc = 0)
        readonly byte[] recvBuffer = new byte[2048];
        Packet packetWork = new Packet();
        Packet pPrev = new Packet();
        Packet pCurr = new Packet();
        bool hasPrev = false;
        bool hasCurr = false;

        float lastRecv = -10f;
        int packetsSeen, packetsRead;
        volatile int packetsIn;
        bool running, calibrated;
        float calStart;
        double sumYaw, sumPitch, sumRoll, sumE, sumDist, sumGazeX, sumGazeY;
        int calN;

        float neutralYaw, neutralPitch, neutralRoll, neutralE, neutralDist;
        Vector2 neutralGaze;

        float turn, pitchTurn, zoom = 1f, zoomTarget = 1f;
        float pitchVal, rollVal, distCmVal;
        Vector2 gazeVal;
        bool blinkVal;
        HandData[] latestHands;
        string lastError;
        TrackingEngine currentEngine;
        bool processPaused = false;

        public int PacketsSeen => packetsSeen;
        public bool ProcessRunning => proc != null && !proc.HasExited;
        public string LastError => lastError;
        public bool FaceVisible => Time.realtimeSinceStartup - lastRecv < 0.4f;

        void Awake()
        {
            Instance = this;
            currentEngine = engine;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) Toggle();
            if (kb != null && kb.f9Key.wasPressedThisFrame && running) StartCalibration();

            if (running && proc != null && proc.HasExited && !processPaused)
            {
                // Nếu MediaPipe gặp sự cố, tự động lùi về YuNet
                if (currentEngine == TrackingEngine.MediaPipe)
                {
                    Say($"MediaPipe dừng ({lastError ?? "lỗi"}), tự động chuyển về YuNet (head_tracker.py)...");
                    currentEngine = TrackingEngine.YuNet;
                    if (!Launch(currentEngine))
                    {
                        Stop();
                    }
                }
                else
                {
                    Say($"Điều khiển bằng đầu: chương trình đã dừng{(string.IsNullOrEmpty(lastError) ? "" : " – " + lastError)}");
                    Stop();
                }
            }

            Packet p = null;
            lock (gate)
            {
                if (packetsIn != packetsRead)
                {
                    p = pCurr;
                    packetsRead = packetsIn;
                }
            }

            if (p != null && running)
            {
                Feed(p);
            }

            // Làm mượt góc xoay và zoom theo thời gian (độc lập FPS máy)
            float dt = Time.unscaledDeltaTime;
            float smoothFactor = 1f - Mathf.Exp(-smoothingRate * dt);

            if (!FaceVisible || !calibrated)
            {
                turn = Mathf.MoveTowards(turn, 0f, 4f * dt);
                pitchTurn = Mathf.MoveTowards(pitchTurn, 0f, 4f * dt);
            }
            if (!running || !FaceVisible)
            {
                zoomTarget = 1f;
            }

            zoom = Mathf.MoveTowards(zoom, zoomTarget, (running && FaceVisible ? 3f : 1f) * dt);
        }

        public void Feed(Packet p)
        {
            if (!running) return;
            packetsSeen++;

            // Chuyển dữ liệu tay cho MediaPipeHandSource
            latestHands = p.hands;
            if (p.hands != null)
            {
                OnHandsReceived?.Invoke(p.hands, p.t);
            }
            OnPacketReceived?.Invoke(p);

            if (p.ok == 0 || (p.e <= 0 && p.dist_cm <= 0))
            {
                turn = 0;
                pitchTurn = 0;
                return;
            }

            lastRecv = Time.realtimeSinceStartup;

            // Đọc các thông số bổ sung
            pitchVal = p.pitch;
            rollVal = p.roll;
            distCmVal = p.dist_cm;
            gazeVal = new Vector2(p.gaze_x, p.gaze_y);
            blinkVal = (p.blink_l != 0 || p.blink_r != 0);

            if (!calibrated)
            {
                sumYaw += p.yaw;
                sumPitch += p.pitch;
                sumRoll += p.roll;
                sumE += p.e;
                sumDist += p.dist_cm;
                sumGazeX += p.gaze_x;
                sumGazeY += p.gaze_y;
                calN++;

                if (Time.realtimeSinceStartup - calStart >= calibrateSeconds && calN >= 10)
                {
                    neutralYaw = (float)(sumYaw / calN);
                    neutralPitch = (float)(sumPitch / calN);
                    neutralRoll = (float)(sumRoll / calN);
                    neutralE = (float)(sumE / calN);
                    neutralDist = (float)(sumDist / calN);
                    neutralGaze = new Vector2((float)(sumGazeX / calN), (float)(sumGazeY / calN));

                    calibrated = true;
                    Status = "Đang điều khiển";
                    Say($"Đã căn ({currentEngine}): quay đầu để xoay, ghé gần để phóng to, ngả xa để nhìn rộng (F9 căn lại, F8 tắt)");
                }
                return;
            }

            // 1. Tính toán góc lái Yaw
            float dy = Mathf.DeltaAngle(neutralYaw, p.yaw);
            float magY = Mathf.Clamp01((Mathf.Abs(dy) - deadZoneDeg) / Mathf.Max(1f, fullTurnDeg - deadZoneDeg));
            float targetTurn = Mathf.Sign(dy) * Mathf.Pow(magY, 1.5f);
            turn = Mathf.Lerp(turn, targetTurn, 1f - Mathf.Exp(-smoothingRate * Time.unscaledDeltaTime));

            // 2. Tính toán nhìn lên/xuống Pitch (nếu bật)
            if (enablePitchLook)
            {
                float dp = p.pitch - neutralPitch;
                float magP = Mathf.Clamp01((Mathf.Abs(dp) - pitchDeadZoneDeg) / Mathf.Max(1f, fullPitchDeg - pitchDeadZoneDeg));
                pitchTurn = Mathf.Sign(dp) * magP * pitchTurnSpeed;
            }
            else
            {
                pitchTurn = 0f;
            }

            // 3. Tính toán Zoom (ưu tiên khoảng cách cm của MediaPipe, fallback sang tỉ lệ e)
            float ratio = 1f;
            if (p.dist_cm > 0 && neutralDist > 0)
            {
                // Càng lại gần webcam thì dist_cm càng nhỏ -> ratio càng lớn -> phóng to
                ratio = neutralDist / Mathf.Max(10f, p.dist_cm);
            }
            else if (p.e > 0 && neutralE > 0)
            {
                ratio = p.e / Mathf.Max(1e-5f, neutralE);
            }

            if (ratio > 1f + zoomDeadZone)
            {
                zoomTarget = 1f + Mathf.Clamp01((ratio - 1f - zoomDeadZone) / 0.4f) * (maxZoom - 1f);
            }
            else if (ratio < 1f - zoomDeadZone)
            {
                zoomTarget = 1f - Mathf.Clamp01((1f - zoomDeadZone - ratio) / 0.25f) * (1f - minZoom);
            }
            else
            {
                zoomTarget = 1f;
            }
        }

        public void Toggle()
        {
            if (running)
            {
                PauseTracking();
                Say("Điều khiển bằng đầu: TẮT (tạm dừng)");
            }
            else
            {
                ResumeTracking();
            }
        }

        void PauseTracking()
        {
            running = false;
            calibrated = false;
            turn = 0f;
            pitchTurn = 0f;
            zoomTarget = 1f;
            Status = "Tắt";

            // Gửi lệnh tạm dừng qua UDP cổng điều khiển (9052)
            GuiLenhDieuKhien("{\"lenh\":\"tam_dung\"}");
            processPaused = true;
        }

        void ResumeTracking()
        {
            if (processPaused && proc != null && !proc.HasExited)
            {
                // Bật lại tức thì (< 1s)
                GuiLenhDieuKhien("{\"lenh\":\"tiep_tuc\"}");
                processPaused = false;
                running = true;
                StartCalibration();
                Say($"Điều khiển bằng đầu: BẬT LẠI ({currentEngine})");
                return;
            }

            currentEngine = engine;
            Begin(launch: true);
        }

        void GuiLenhDieuKhien(string jsonCmd)
        {
            try
            {
                using (var client = new UdpClient())
                {
                    byte[] data = Encoding.UTF8.GetBytes(jsonCmd);
                    client.Send(data, data.Length, "127.0.0.1", port + 1);
                }
            }
            catch { }
        }

        public void Begin(bool launch = true)
        {
            if (running) return;
            lastError = null;

            if (udp == null)
            {
                try
                {
                    udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                }
                catch (Exception ex)
                {
                    Say($"Điều khiển bằng đầu: không mở được cổng {port} ({ex.Message})");
                    return;
                }
            }

            listening = true;
            if (thread == null || !thread.IsAlive)
            {
                thread = new Thread(Listen) { IsBackground = true, Name = "KhktHeadTracking" };
                thread.Start();
            }

            running = true;
            processPaused = false;

            if (launch && !Launch(currentEngine))
            {
                Stop();
                return;
            }

            StartCalibration();
        }

        public void StartCalibration()
        {
            calibrated = false;
            calStart = Time.realtimeSinceStartup;
            sumYaw = sumPitch = sumRoll = sumE = sumDist = sumGazeX = sumGazeY = 0;
            calN = 0;
            turn = 0;
            pitchTurn = 0;
            zoomTarget = 1f;
            Status = "Đang căn";
            Say($"Điều khiển bằng đầu ({currentEngine}): BẬT – nhìn thẳng màn hình 2 giây để căn tư thế");
        }

        bool Launch(TrackingEngine eng)
        {
            string pyExe = FindPython();
            if (string.IsNullOrEmpty(pyExe))
            {
                Say("Không tìm thấy Python (.venv/bin/python, python3, python)");
                return false;
            }

            string script = FindScript(eng);
            if (string.IsNullOrEmpty(script))
            {
                Say($"Không tìm thấy file script cho {eng}");
                return false;
            }

            try
            {
                string args = $"\"{script}\" --cong {port} --cam {cameraIndex} --cha {Process.GetCurrentProcess().Id} --che-do {cheDo}";
                var psi = new ProcessStartInfo(pyExe, args)
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
                    if (!string.IsNullOrWhiteSpace(a.Data) && !a.Data.Contains("WARN") && !a.Data.Contains("INFO"))
                    {
                        lastError = a.Data.Trim();
                    }
                };
                proc.OutputDataReceived += (_, _) => { };
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                Debug.Log($"[KHKT-HEAD] Đã chạy {pyExe} {args}");
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                return false;
            }
        }

        string FindPython()
        {
            if (!string.IsNullOrEmpty(pythonPath) && File.Exists(pythonPath))
                return Path.GetFullPath(pythonPath);

            var env = System.Environment.GetEnvironmentVariable("KHKT_PYTHON");
            if (!string.IsNullOrEmpty(env) && File.Exists(env))
                return Path.GetFullPath(env);

            // Tìm trong repo/.venv
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string venvMac = Path.Combine(root, ".venv", "bin", "python");
            if (File.Exists(venvMac)) return venvMac;

            string venvWin = Path.Combine(root, ".venv", "Scripts", "python.exe");
            if (File.Exists(venvWin)) return venvWin;

            // Tìm cạnh file .exe
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(exeDir))
            {
                string exePyMac = Path.Combine(exeDir, ".venv", "bin", "python");
                if (File.Exists(exePyMac)) return exePyMac;
                string exePyWin = Path.Combine(exeDir, ".venv", "Scripts", "python.exe");
                if (File.Exists(exePyWin)) return exePyWin;
            }

            // Thử lệnh hệ thống
            foreach (var cmd in new[] { "python3", "python", "py" })
            {
                try
                {
                    using (var p = Process.Start(new ProcessStartInfo(cmd, "--version") { UseShellExecute = false, CreateNoWindow = true }))
                    {
                        p.WaitForExit(1000);
                        if (p.ExitCode == 0) return cmd;
                    }
                }
                catch { }
            }

            return null;
        }

        string FindScript(TrackingEngine eng)
        {
            string scriptName = (eng == TrackingEngine.MediaPipe) ? "mediapipe_tracker.py" : "head_tracker.py";
            if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                return Path.GetFullPath(scriptPath);

            var tried = new System.Collections.Generic.List<string>
            {
                Path.Combine(Application.dataPath, "..", "..", "tools", scriptName),
                Path.Combine(Application.dataPath, "..", "tools", scriptName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", scriptName)
            };

            foreach (var p in tried)
            {
                if (File.Exists(p)) return Path.GetFullPath(p);
            }
            return null;
        }

        void Listen()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (listening)
            {
                try
                {
                    var data = udp.Receive(ref any);
                    if (data == null || data.Length == 0) continue;

                    string json = Encoding.UTF8.GetString(data);
                    // Dùng FromJsonOverwrite để tái sử dụng instance, 0 GC Alloc
                    JsonUtility.FromJsonOverwrite(json, packetWork);

                    lock (gate)
                    {
                        // Swap buffer
                        var temp = pPrev;
                        pPrev = pCurr;
                        pCurr = packetWork;
                        packetWork = temp;

                        hasPrev = hasCurr;
                        hasCurr = true;
                        packetsIn++;
                    }
                }
                catch (SocketException)
                {
                    if (!listening) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch { }
            }
        }

        public void Stop()
        {
            running = false;
            calibrated = false;
            listening = false;
            processPaused = false;
            turn = 0;
            pitchTurn = 0;
            zoomTarget = 1f;
            Status = "Tắt";

            try { udp?.Close(); } catch { }
            udp = null;

            try
            {
                if (proc != null && !proc.HasExited) proc.Kill();
            }
            catch { }
            proc = null;
            thread = null;
        }

        void OnDisable() => Stop();
        void OnApplicationQuit() => Stop();

        static void Say(string s)
        {
            LabMessages.Say(s);
            Debug.Log("[KHKT-HEAD] " + s);
        }
    }
}
