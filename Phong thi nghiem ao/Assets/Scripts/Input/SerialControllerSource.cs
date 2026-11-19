#if NET_4_6
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Khkt.Input
{
    /// <summary>
    /// Đọc tay cầm qua cổng COM (cáp USB hoặc Bluetooth) trên một luồng riêng (không làm giật khung hình):
    /// tay cầm Arduino Mega, hoặc ESP32-CAM + 1 mạch joystick (firmware/tay_cam_esp32cam). Chạy bộ lọc Mahony theo đúng
    /// mốc thời gian của từng mẫu (khi có MPU6050), rồi đẩy mẫu về luồng chính.
    /// portName = "auto": tự thử từng cổng COM, giữ cổng đầu tiên gửi gói hợp lệ; rút cáp thì tự dò lại.
    /// Cần Player Settings > Api Compatibility Level = .NET Framework để có System.IO.Ports.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SerialControllerSource : ControllerSource
    {
        [Tooltip("\"auto\" để tự dò, hoặc ghi cổng cụ thể như COM5 (xem Device Manager hoặc: python tools/tay_cam_monitor.py --liet-ke)")]
        public string portName = "auto";
        public int baudRate = 115200;
        [Tooltip("Bật DTR khi mở cổng. Arduino Mega sẽ tự khởi động lại; ESP32 cắm đế nạp ESP32-CAM-MB phải để TẮT (bật có thể làm ESP32 vào chế độ nạp)")]
        public bool dtrEnable;
        [Tooltip("Đảo trục Y joystick nếu đẩy lên mà nhân vật đi lùi")]
        public bool invertJoy1Y, invertJoy2Y;
        [Range(0f, 0.3f)] public float joystickDeadzone = 0.08f;
        public bool logFirmwareMessages = true;

        public PacketParser Parser => parser;
        public override bool IsConnected => connected && Time.realtimeSinceStartup - lastSampleTime < 0.5f;
        public override string ControllerName => connected ? openPort ?? "" : "";
        public int PacketsPerSecond { get; private set; }
        public override string StatusText =>
            IsConnected ? $"Cổng COM: đã nối {openPort}, {PacketsPerSecond} gói/s"
            : AutoDetect ? $"Cổng COM: đang dò tay cầm ({(string.IsNullOrEmpty(scanInfo) ? "chưa có cổng COM nào" : scanInfo)})"
            : $"Cổng COM: chưa đọc được {portName} {lastError}";

        bool AutoDetect => string.IsNullOrWhiteSpace(portName) || portName.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);

        static readonly Stopwatch Clock = Stopwatch.StartNew();
        readonly ConcurrentQueue<ControllerState> samples = new ConcurrentQueue<ControllerState>();
        readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        readonly MahonyFilter filter = new MahonyFilter();
        readonly Dictionary<string, double> skipUntil = new Dictionary<string, double>();
        volatile PacketParser parser = new PacketParser();
        volatile string openPort, scanInfo = "", lastError = "";
        Thread thread;
        volatile bool running, connected, resetRequested = true, newPort, otherConnected;
        float lastSampleTime = -10f, heartbeatTimer, rateTimer;
        int rateCount, lastVibration = -1;
        string lastLeds;

        void OnEnable()
        {
            running = true;
            thread = new Thread(ReadLoop) { IsBackground = true, Name = "KhktSerial" };
            thread.Start();
            PadInput.Register(this);
        }

        void OnDisable()
        {
            PadInput.Unregister(this);
            outgoing.Enqueue("r0\n");  // tay cầm VERA: thôi phát gói thô
            outgoing.Enqueue("R0\n");
            outgoing.Enqueue("L0,0\n");
            Thread.Sleep(30);
            running = false;
            thread?.Join(1500);
        }

        void Update()
        {
            // Đang dùng tay cầm khác (Wi-Fi): thôi dò cổng COM, để yên cổng USB của Mega / ESP32 (đang cắm chỉ để lấy nguồn
            // hoặc để mở Serial Monitor)
            bool other = false;
            foreach (var src in PadInput.Sources)
                if (src != this && src != null && src.IsConnected) other = true;
            otherConnected = other;
            while (samples.TryDequeue(out var s))
            {
                lastSampleTime = Time.realtimeSinceStartup;
                rateCount++;
                Publish(s);
            }
            while (parser.Messages.TryDequeue(out var m))
                if (logFirmwareMessages) Debug.Log($"[Tay cầm {openPort}] {m}");
            if (newPort)
            {
                newPort = false;
                lastVibration = -1;
                lastLeds = null;
                Debug.Log($"[Tay cầm] Đã nối tay cầm ở {openPort}");
                outgoing.Enqueue("I\n");
            }

            heartbeatTimer += Time.unscaledDeltaTime;
            if (heartbeatTimer > 0.4f)
            {
                heartbeatTimer = 0;
                outgoing.Enqueue("H\n");  // firmware tự tắt rung nếu 2 s không nghe PC
            }
            rateTimer += Time.unscaledDeltaTime;
            if (rateTimer >= 1f)
            {
                PacketsPerSecond = Mathf.RoundToInt(rateCount / rateTimer);
                rateCount = 0;
                rateTimer = 0;
            }
        }

        public override void SetVibration(int level)
        {
            level = Mathf.Clamp(level, 0, 255);
            if (level == lastVibration) return;
            lastVibration = level;
            outgoing.Enqueue($"R{level}\n");
        }

        public override void Pulse(int level, int ms) =>
            outgoing.Enqueue($"P{Mathf.Clamp(level, 0, 255)},{Mathf.Clamp(ms, 0, 2000)}\n");

        public override void SetLeds(bool green, bool red)
        {
            string cmd = $"L{(green ? 1 : 0)},{(red ? 1 : 0)}\n";
            if (cmd == lastLeds) return;
            lastLeds = cmd;
            outgoing.Enqueue(cmd);
        }

        public override void ResetOrientation() => resetRequested = true;

        /// <summary>Hiệu chuẩn lại con quay: người dùng phải để yên tay cầm khoảng 2 giây.</summary>
        public void CalibrateGyro() => outgoing.Enqueue("C\n");
        /// <summary>Lấy vị trí nghỉ hiện tại của joystick làm tâm (bản ESP32): buông tay khỏi cần.</summary>
        public void CenterJoysticks() => outgoing.Enqueue("J\n");

        /// <summary>Tốc độ cổng thử thêm khi baudRate không thấy tay cầm: firmware tay cầm VERA (từ 18/11/2026) chạy 921600 baud.</summary>
        public int veraBaudRate = 921600;
        int openBaud;

        SerialPort Open(string name) => Open(name, openBaud > 0 ? openBaud : baudRate);

        SerialPort Open(string name, int baud)
        {
            var p = new SerialPort(name, baud) { ReadTimeout = 50, WriteTimeout = 50, DtrEnable = dtrEnable, RtsEnable = false };
            p.Open();
            return p;
        }

        /// <summary>
        /// Thử lần lượt các cổng COM, trả về cổng đầu tiên gửi gói hợp lệ (đang mở sẵn). Tay cầm cũ tự phát gói 29 byte;
        /// tay cầm VERA chỉ phát gói nhị phân sau lệnh "r2", và mở cổng có thể làm ESP32 khởi động lại + cân con quay 2 s,
        /// nên gửi "r2" vài lần và chờ tối đa 6 s nếu cổng đang in dòng '#' của firmware VERA.
        /// </summary>
        SerialPort Scan()
        {
            var names = SerialPort.GetPortNames().Distinct().OrderBy(n => n.Length).ThenBy(n => n).ToArray();
            var tried = new List<string>();
            var buffer = new byte[256];
            var found = new List<RawPacket>();
            var foundVera = new List<VeraSample>();
            foreach (var name in names)
            {
                if (!running) return null;
                if (skipUntil.TryGetValue(name, out double until) && Clock.Elapsed.TotalSeconds < until) { tried.Add(name); continue; }
                foreach (int baud in veraBaudRate > 0 && veraBaudRate != baudRate ? new[] { baudRate, veraBaudRate } : new[] { baudRate })
                {
                SerialPort p = null;
                try
                {
                    p = Open(name, baud);
                    var probe = new PacketParser();
                    found.Clear();
                    foundVera.Clear();
                    var t0 = Clock.Elapsed.TotalSeconds;
                    double lastAsk = -10;
                    bool veraText = false;
                    while (found.Count < 3 && foundVera.Count < 2 && running)
                    {
                        double el = Clock.Elapsed.TotalSeconds - t0;
                        if (el > 6.0 || (el > 1.5 && !veraText && probe.VeraPackets == 0)) break;
                        if (el - lastAsk > 1.2)
                        {
                            lastAsk = el;
                            try { p.Write("r2\n"); } catch (Exception) { }
                        }
                        try { probe.Feed(buffer, p.Read(buffer, 0, buffer.Length), found, foundVera); }
                        catch (TimeoutException) { }
                        while (probe.Messages.TryDequeue(out var m))
                            if (m.Contains("VERA") || m.StartsWith("THO") || m.StartsWith("CAL") || m.StartsWith("INFO")) veraText = true;
                    }
                    if (found.Count >= 3 || foundVera.Count >= 2) { openBaud = baud; return p; }
                }
                catch (Exception) { }  // cổng đang bị chương trình khác dùng (Arduino IDE...), cổng Bluetooth chưa ghép đôi...
                try { p?.Close(); } catch (Exception) { }
                }
                skipUntil[name] = Clock.Elapsed.TotalSeconds + 8;  // cổng không có tay cầm: 8 s sau mới thử lại
                tried.Add(name);
            }
            scanInfo = tried.Count > 0 ? string.Join(", ", tried) + " không có tay cầm" : "";
            return null;
        }

        void ReadLoop()
        {
            var buffer = new byte[512];
            var packets = new List<RawPacket>();
            var vera = new List<VeraSample>();
            double lastAskRaw = 0;
            SerialPort port = null;
            uint lastT = 0;
            bool haveT = false, warned = false;
            double lastPacket = 0;
            while (running)
            {
                try
                {
                    if (port == null || !port.IsOpen)
                    {
                        if (AutoDetect && otherConnected)
                        {
                            scanInfo = "tạm dừng vì đang dùng tay cầm Wi-Fi";
                            Thread.Sleep(1000);
                            continue;
                        }
                        port = AutoDetect ? Scan() : Open(portName.Trim());
                        if (port == null)
                        {
                            Thread.Sleep(1500);
                            continue;
                        }
                        openPort = port.PortName;
                        parser = new PacketParser();
                        connected = true;
                        haveT = false;
                        resetRequested = true;
                        newPort = true;
                        warned = false;
                        lastError = "";
                        lastPacket = Clock.Elapsed.TotalSeconds;
                    }
                    while (outgoing.TryDequeue(out var cmd)) port.Write(cmd);

                    int n;
                    try { n = port.Read(buffer, 0, buffer.Length); }
                    catch (TimeoutException)
                    {
                        // Tự dò mà cổng im quá 6 s (tay cầm treo, cổng Bluetooth mất kết nối): đóng và dò lại
                        if (AutoDetect && Clock.Elapsed.TotalSeconds - lastPacket > 6) throw new TimeoutException("không nhận được gói nào trong 6 s");
                        AskRaw(port, ref lastAskRaw, lastPacket);
                        continue;
                    }

                    packets.Clear();
                    vera.Clear();
                    parser.Feed(buffer, n, packets, vera);
                    if (packets.Count > 0 || vera.Count > 0) lastPacket = Clock.Elapsed.TotalSeconds;
                    else AskRaw(port, ref lastAskRaw, lastPacket);
                    // Tay cầm VERA: hướng đã tính trên mạch (chống trôi yaw, bù nhiệt), không chạy Mahony trên máy tính
                    foreach (var v in vera) samples.Enqueue(ControllerPackets.FromVera(v));
                    foreach (var p in packets)
                    {
                        var s = ControllerPackets.ToState(p, joystickDeadzone, invertJoy1Y, invertJoy2Y);
                        float dt = haveT ? Mathf.Clamp((p.TimeMs - lastT) / 1000f, 0.001f, 0.05f) : 0.01f;
                        lastT = p.TimeMs;
                        haveT = true;
                        if (!s.Pressed(ControllerButtons.ImuError))
                        {
                            if (resetRequested)
                            {
                                filter.ResetFromAccel(s.AccelG);
                                resetRequested = false;
                            }
                            filter.Update(s.GyroDps, s.AccelG, dt);
                            s.OrientationRH = filter.OrientationRH;
                        }
                        else s.OrientationRH = Quaternion.identity;
                        samples.Enqueue(s);
                    }
                }
                catch (Exception e)
                {
                    // Rút cáp, sai cổng, cổng đang bị Arduino IDE chiếm...: đóng và thử lại
                    connected = false;
                    lastError = e.Message;
                    try { port?.Close(); } catch (Exception) { }
                    port = null;
                    if (!warned) Debug.LogWarning($"[Tay cầm] {openPort ?? portName}: {e.Message}. Thử lại.");
                    warned = true;
                    Thread.Sleep(1000);
                }
            }
            try { port?.Close(); } catch (Exception) { }
        }

        /// <summary>Tay cầm VERA im quá 2 s (vừa khởi động lại, mất lệnh): xin lại gói thô "r2", tối đa 2 s một lần.</summary>
        static void AskRaw(SerialPort port, ref double lastAsk, double lastPacket)
        {
            double now = Clock.Elapsed.TotalSeconds;
            if (now - lastPacket > 2 && now - lastAsk > 2)
            {
                lastAsk = now;
                port.Write("r2\n");
            }
        }
    }
}
#else
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Bản thay thế khi project để Api Compatibility Level = .NET Standard (không có System.IO.Ports).
    /// Đổi sang .NET Framework: Edit > Project Settings > Player > Other Settings > Api Compatibility Level.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SerialControllerSource : ControllerSource
    {
        public string portName = "auto";
        public int baudRate = 115200;
        public bool dtrEnable;
        public bool invertJoy1Y, invertJoy2Y;
        [Range(0f, 0.3f)] public float joystickDeadzone = 0.08f;
        public bool logFirmwareMessages = true;

        public override bool IsConnected => false;
        public override string StatusText => "Cổng COM: cần Api Compatibility Level = .NET Framework";

        void OnEnable() => Debug.LogError(
            "[Tay cầm] Cần Api Compatibility Level = .NET Framework để đọc cổng COM " +
            "(Project Settings > Player > Other Settings).");

        public override void SetVibration(int level) { }
        public override void Pulse(int level, int ms) { }
        public override void SetLeds(bool green, bool red) { }
        public override void ResetOrientation() { }
        public void CalibrateGyro() { }
        public void CenterJoysticks() { }
    }
}
#endif
