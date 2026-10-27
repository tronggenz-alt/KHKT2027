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
    /// <summary>
    /// Điều khiển góc nhìn bằng ĐẦU qua webcam (thử nghiệm):
    ///  • F8 bật / tắt: Unity tự chạy tools/head_tracker.py (Python + OpenCV, nhận diện mặt YuNet) ở chế độ ngầm và tắt khi
    ///    thoát; chương trình đó gửi góc quay đầu, khoảng cách (qua độ rộng hai mắt) qua UDP cổng 9051.
    ///  • Vừa bật (hoặc F9): nhìn thẳng màn hình 2 giây để căn tư thế giữa.
    ///  • Kiểu lái: quay đầu quá ~30° thì góc nhìn xoay liên tục về phía đó, tới 50° xoay nhanh nhất; nhìn thẳng thì dừng.
    ///  • Ghé gần webcam thì phóng to (thu hẹp góc nhìn camera, tối đa ×2,5), ngả ra xa thì nhìn rộng ra (tới ×0,8).
    ///  • Mất mặt quá 0,4 s (quay đi, che mặt): thôi xoay, zoom từ từ về bình thường.
    /// DesktopViewer cộng Turn vào góc xoay và chia góc nhìn camera cho Zoom, cùng chỗ với chuột và tay cầm.
    /// </summary>
    public class HeadTracking : MonoBehaviour
    {
        public static HeadTracking Instance { get; private set; }

        public int port = 9051;
        [Tooltip("Đường dẫn head_tracker.py; để trống thì tự tìm (thư mục tools cạnh project / cạnh file .exe)")]
        public string scriptPath = "";
        public int cameraIndex = 0;
        [Header("Kiểu lái")]
        [Tooltip("Quay đầu dưới góc này thì không xoay (người dùng: 12° nhiễu quá, nâng lên 30°)")]
        public float deadZoneDeg = 30f;
        public float fullTurnDeg = 50f;
        [Header("Zoom")]
        [Tooltip("Lệch khoảng cách (tỉ lệ) chưa tính là ghé gần / ngả xa")]
        public float zoomDeadZone = 0.07f;
        public float maxZoom = 2.5f;
        public float minZoom = 0.8f;
        public float calibrateSeconds = 2f;

        // ---- kết quả cho DesktopViewer ----
        /// <summary>Đang điều khiển bằng đầu (đã bật, đã căn).</summary>
        public static bool Active => Instance != null && Instance.running && Instance.calibrated;
        /// <summary>Tốc độ xoay -1..1 (dương: sang phải).</summary>
        public static float Turn => Instance != null ? Instance.turn : 0f;
        /// <summary>Hệ số phóng to (1: bình thường); góc nhìn camera = góc gốc / Zoom.</summary>
        public static float Zoom => Instance != null ? Instance.zoom : 1f;
        public string Status { get; private set; } = "Tắt";

        [Serializable]
        class Packet { public int ok; public float yaw; public float pitch; public float e; public double t; }

        Process proc;
        UdpClient udp;
        Thread thread;
        volatile bool listening;
        readonly object gate = new object();
        Packet last;
        float lastRecv = -10f;          // Time.realtimeSinceStartup của gói có mặt gần nhất (đặt ở luồng chính)
        int packetsSeen, packetsRead;
        volatile int packetsIn;
        bool running, calibrated;
        float calStart;
        double sumYaw, sumE;
        int calN;
        float neutralYaw, neutralE;
        float turn, zoom = 1f, zoomTarget = 1f;
        string lastError;

        /// <summary>Số gói đã nhận (kể cả gói không thấy mặt) và chương trình nhận diện còn chạy: để kiểm thử.</summary>
        public int PacketsSeen => packetsSeen;
        public bool ProcessRunning => proc != null && !proc.HasExited;
        public string LastError => lastError;

        /// <summary>Đang có mặt trong khung hình (gói ok gần đây).</summary>
        public bool FaceVisible => Time.realtimeSinceStartup - lastRecv < 0.4f;

        void Awake() => Instance = this;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) Toggle();
            if (kb != null && kb.f9Key.wasPressedThisFrame && running) StartCalibration();

            if (running && proc != null && proc.HasExited)
            {
                Say($"Điều khiển bằng đầu: chương trình nhận diện đã dừng{(string.IsNullOrEmpty(lastError) ? "" : " – " + lastError)}");
                Stop();
            }
            Packet p = null;
            lock (gate)
            {
                if (packetsIn != packetsRead) { p = last; packetsRead = packetsIn; }
            }
            if (p != null && running) Feed(p);

            if (!FaceVisible || !calibrated) turn = 0;
            if (!running || !FaceVisible) zoomTarget = 1f;
            zoom = Mathf.MoveTowards(zoom, zoomTarget, (running && FaceVisible ? 3f : 1f) * Time.unscaledDeltaTime);
        }

        void Feed(Packet p) => Feed(p.ok != 0, p.yaw, p.e);

        /// <summary>Xử lý một mẫu (luồng chính). Kiểm thử gọi trực tiếp, không cần webcam.</summary>
        public void Feed(bool ok, float yaw, float e)
        {
            if (!running) return;
            packetsSeen++;
            if (!ok || e <= 0)
            {
                turn = 0;  // chương trình nhận diện báo không thấy mặt: thôi xoay ngay
                return;
            }
            lastRecv = Time.realtimeSinceStartup;
            if (!calibrated)
            {
                sumYaw += yaw;
                sumE += e;
                calN++;
                if (Time.realtimeSinceStartup - calStart >= calibrateSeconds && calN >= 10)
                {
                    neutralYaw = (float)(sumYaw / calN);
                    neutralE = (float)(sumE / calN);
                    calibrated = true;
                    Status = "Đang điều khiển";
                    Say("Đã căn: quay đầu trái / phải để xoay, ghé gần màn hình để phóng to, ngả ra xa để nhìn rộng (F9: căn lại, F8: tắt)");
                }
                return;
            }
            float dy = Mathf.DeltaAngle(neutralYaw, yaw);
            float mag = Mathf.Clamp01((Mathf.Abs(dy) - deadZoneDeg) / Mathf.Max(1f, fullTurnDeg - deadZoneDeg));
            turn = Mathf.Sign(dy) * Mathf.Pow(mag, 1.5f);
            float r = e / Mathf.Max(1e-5f, neutralE);
            if (r > 1f + zoomDeadZone) zoomTarget = 1f + Mathf.Clamp01((r - 1f - zoomDeadZone) / 0.4f) * (maxZoom - 1f);
            else if (r < 1f - zoomDeadZone) zoomTarget = 1f - Mathf.Clamp01((1f - zoomDeadZone - r) / 0.25f) * (1f - minZoom);
            else zoomTarget = 1f;
        }

        public void Toggle()
        {
            if (running) { Stop(); Say("Điều khiển bằng đầu: TẮT"); }
            else Begin();
        }

        /// <summary>Bật: mở cổng UDP, chạy chương trình nhận diện (launch = false: chỉ nghe, dùng khi tự chạy Python hoặc kiểm thử).</summary>
        public void Begin(bool launch = true)
        {
            if (running) return;
            lastError = null;
            try
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            }
            catch (Exception ex)
            {
                Say($"Điều khiển bằng đầu: không mở được cổng {port} ({ex.Message})");
                return;
            }
            listening = true;
            thread = new Thread(Listen) { IsBackground = true, Name = "KhktHeadTracking" };
            thread.Start();
            running = true;
            if (launch && !Launch())
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
            sumYaw = sumE = 0;
            calN = 0;
            turn = 0;
            zoomTarget = 1f;
            Status = "Đang căn";
            Say("Điều khiển bằng đầu: BẬT – nhìn thẳng vào màn hình 2 giây để căn tư thế giữa");
        }

        bool Launch()
        {
            string script = FindScript();
            if (script == null)
            {
                Say("Điều khiển bằng đầu: không tìm thấy tools/head_tracker.py");
                return false;
            }
            foreach (var exe in new[] { "python", "py" })
            {
                try
                {
                    var psi = new ProcessStartInfo(exe, $"\"{script}\" --cong {port} --cam {cameraIndex} --cha {Process.GetCurrentProcess().Id}")
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
                    if (proc == null) continue;
                    proc.ErrorDataReceived += (_, a) => { if (!string.IsNullOrWhiteSpace(a.Data) && !a.Data.Contains("WARN")) lastError = a.Data.Trim(); };
                    proc.OutputDataReceived += (_, a) => { };
                    proc.BeginErrorReadLine();
                    proc.BeginOutputReadLine();
                    Debug.Log($"[KHKT-DAU] Chạy {exe} {script}");
                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }
            Say("Điều khiển bằng đầu: không chạy được Python (cần cài Python + opencv-python)");
            return false;
        }

        string FindScript()
        {
            var tried = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(scriptPath)) tried.Add(scriptPath);
            var env = System.Environment.GetEnvironmentVariable("KHKT_HEAD_TRACKER");
            if (!string.IsNullOrEmpty(env)) tried.Add(env);
            // trình soạn thảo: <repo>/Phong thi nghiem ao/Assets → <repo>/tools; bản .exe: <thư mục exe>/PhongThiNghiemAo_Data → <thư mục exe>/tools
            tried.Add(Path.Combine(Application.dataPath, "..", "..", "tools", "head_tracker.py"));
            tried.Add(Path.Combine(Application.dataPath, "..", "tools", "head_tracker.py"));
            foreach (var p in tried)
                if (File.Exists(p)) return Path.GetFullPath(p);
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
                    var p = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(data));
                    if (p == null) continue;
                    lock (gate)
                    {
                        last = p;
                        packetsIn++;
                    }
                }
                catch (SocketException) { if (!listening) break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { }
            }
        }

        public void Stop()
        {
            running = false;
            calibrated = false;
            listening = false;
            turn = 0;
            zoomTarget = 1f;
            Status = "Tắt";
            try { udp?.Close(); } catch { }
            udp = null;
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
            proc = null;
            thread = null;
        }

        void OnDisable() => Stop();
        void OnApplicationQuit() => Stop();

        static void Say(string s)
        {
            LabMessages.Say(s);
            Debug.Log("[KHKT-DAU] " + s);
        }
    }
}
