using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Khkt.Input
{
    /// <summary>
    /// Tay cầm không dây qua Wi-Fi UDP (cổng listenPort). Nhận hai dạng gói:
    ///   - gói nhị phân 29 byte giống tay cầm dây (docs/02-giao-thuc-tay-cam.md), kèm lệnh rung / đèn / nhịp tim gửi lại;
    ///   - dòng chữ "X,Y,B" của bộ joystick → Arduino Mega → ESP32 (firmware/joystick_qua_mega, docs/09): X, Y là giá trị
    ///     analogRead (0-1023 của Mega, hoặc 0-4095 nếu đọc thẳng bằng ESP32), B = 1 khi nhấn cần. Tâm cần lấy theo
    ///     các mẫu đầu tiên (đừng chạm cần lúc bật), biên độ tự học dần khi đẩy cần hết cỡ. Dạng này là tay cầm 1 cần:
    ///     PadInput dùng 2 chế độ ĐI / TAY. Dòng "NO_MEGA_DATA" nghĩa là ESP32 sống nhưng không nhận được gì từ Mega.
    /// Lần đầu chạy, Windows hỏi cho phép qua tường lửa: chọn cho phép (cả mạng Riêng tư / Private).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UdpControllerSource : ControllerSource
    {
        [Tooltip("Cổng máy tính nghe gói tay cầm (CONG_PC trong cau_hinh.h)")]
        public int listenPort = 4210;
        [Tooltip("Để trống: tự tìm tay cầm qua gói quảng bá. Mạng chặn quảng bá thì điền IP tay cầm (in ra Serial Monitor khi khởi động)")]
        public string controllerAddress = "";
        [Tooltip("Cổng tay cầm nghe lệnh (CONG_TAY_CAM), chỉ dùng khi điền controllerAddress")]
        public int controllerPort = 4211;
        [Tooltip("Đảo trục Y joystick nếu đẩy lên mà nhân vật đi lùi")]
        public bool invertJoy1Y, invertJoy2Y;
        [Tooltip("Mạch joystick (X,Y,B): đảo trục X nếu đẩy phải mà nhân vật quay trái")]
        public bool invertJoy2X;
        [Tooltip("Mạch joystick (X,Y,B): hoán đổi X và Y nếu mạch gắn xoay ngang (đẩy lên mà nhân vật quay)")]
        public bool swapJoy2Axes;
        [Range(0f, 0.3f)] public float joystickDeadzone = 0.08f;
        public bool logFirmwareMessages = true;

        public PacketParser Parser => parser;
        public override bool IsConnected => Time.realtimeSinceStartup - lastSampleTime < 0.5f;
        /// <summary>Địa chỉ IP:cổng của tay cầm đang nối (hoặc nối lần cuối), rỗng nếu chưa thấy tay cầm nào.</summary>
        public override string ControllerName => controllerName;
        public override string StatusText =>
            !string.IsNullOrEmpty(PortError) ? $"Wi-Fi: không mở được cổng UDP {listenPort} ({PortError})"
            : IsConnected ? $"Wi-Fi: đã nối {controllerName}, {PacketsPerSecond} gói/s" +
                            (textFormat ? $", dạng X,Y,B (tâm {xyb.CenterX:0},{xyb.CenterY:0})" : "")
            : Time.realtimeSinceStartup - noMegaTime < 2f ? $"Wi-Fi: ESP32 {controllerName} không nhận được dữ liệu từ Mega (kiểm tra dây D22 → IO13 và GND chung)"
            : !string.IsNullOrEmpty(controllerName) ? $"Wi-Fi: mất tín hiệu từ {controllerName}"
            : $"Wi-Fi: đang chờ trên cổng UDP {listenPort}";
        /// <summary>Tay cầm đang gửi dạng chữ "X,Y,B" (bộ joystick → Mega → ESP32).</summary>
        public bool TextFormat => textFormat;
        volatile bool textFormat;
        readonly XybDecoder xyb = new XybDecoder();
        float noMegaTime = -10f;
        volatile bool noMegaFlag;
        string controllerName = "";
        public int PacketsPerSecond { get; private set; }
        public string LastFirmwareMessage { get; private set; } = "";
        /// <summary>Lỗi mở cổng (ví dụ cổng đang bị tools/tay_cam_udp.py chiếm), rỗng nếu ổn.</summary>
        public string PortError => portError ?? "";
        /// <summary>Tay cầm không có hoặc lỗi MPU6050: chỉ dùng joystick và nút.</summary>
        public bool ImuMissing => Latest.Pressed(ControllerButtons.ImuError);

        static readonly Stopwatch Clock = Stopwatch.StartNew();
        readonly ConcurrentQueue<ControllerState> samples = new ConcurrentQueue<ControllerState>();
        readonly MahonyFilter filter = new MahonyFilter();
        readonly object gate = new object();
        volatile PacketParser parser = new PacketParser();
        volatile UdpClient client;
        volatile string portError;
        volatile bool running, resetRequested = true, newRemote;
        IPEndPoint remote, fixedRemote;
        double lastHeardS = -10;
        Thread thread;
        float lastSampleTime = -10f, heartbeatTimer, rateTimer;
        int rateCount, lastVibration = -1;
        string lastLeds;

        /// <summary>
        /// Cảnh dựng trước khi có tay cầm Wi-Fi (chưa chạy lại menu KHKT > Dựng phòng thí nghiệm) vẫn nhận được tay cầm:
        /// có người xem chuột-bàn phím (DesktopViewer) mà chưa có nguồn này thì tự tạo.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInDesktopScene()
        {
            if (FindAnyObjectByType<Khkt.Environment.DesktopViewer>() == null || FindAnyObjectByType<UdpControllerSource>() != null) return;
            new GameObject("Tay cầm Wi-Fi (tự tạo)").AddComponent<UdpControllerSource>();
        }

        void OnEnable()
        {
            // Bản .exe: chạy "PhongThiNghiemAo.exe -taycam 192.168.1.50" để gọi thẳng tay cầm khi mạng chặn gói quảng bá
            string address = controllerAddress;
            var args = System.Environment.GetCommandLineArgs();
            int k = Array.IndexOf(args, "-taycam");
            if (k >= 0 && k + 1 < args.Length) address = args[k + 1];
            fixedRemote = null;
            if (!string.IsNullOrWhiteSpace(address) && IPAddress.TryParse(address.Trim(), out var ip))
                fixedRemote = new IPEndPoint(ip, controllerPort);
            running = true;
            thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "KhktUdpController" };
            thread.Start();
            PadInput.Register(this);
        }

        void OnDisable()
        {
            PadInput.Unregister(this);
            Send("R0\nL0,0\n");
            running = false;
            client?.Close();
            thread?.Join(300);
            client = null;
        }

        void Update()
        {
            while (samples.TryDequeue(out var s))
            {
                lastSampleTime = Time.realtimeSinceStartup;
                rateCount++;
                Publish(s);
            }
            while (parser.Messages.TryDequeue(out var m))
            {
                LastFirmwareMessage = m;
                if (logFirmwareMessages) Debug.Log($"[Tay cầm Wi-Fi] {m}");
            }
            if (newRemote)
            {
                newRemote = false;
                lastVibration = -1;  // tay cầm mới (hoặc vừa khởi động lại): gửi lại trạng thái rung, đèn
                lastLeds = null;
                lock (gate) controllerName = remote != null ? remote.ToString() : "";
                Debug.Log($"[Tay cầm Wi-Fi] Đã thấy tay cầm {controllerName}");
                if (!textFormat) Send("I\n");
            }
            if (noMegaFlag)
            {
                noMegaFlag = false;
                if (Time.realtimeSinceStartup - noMegaTime > 5f && logFirmwareMessages)
                    Debug.LogWarning($"[Tay cầm Wi-Fi] {controllerName}: ESP32 báo NO_MEGA_DATA (không nhận được dữ liệu từ Mega)");
                noMegaTime = Time.realtimeSinceStartup;
            }

            heartbeatTimer += Time.unscaledDeltaTime;
            if (heartbeatTimer > 0.4f && !textFormat)  // bộ Mega → ESP32 chỉ gửi một chiều, không cần nhịp tim
            {
                heartbeatTimer = 0;
                Send("H\n");  // tay cầm tự tắt rung nếu 2 s không nghe máy tính, 5 s thì quay lại phát quảng bá
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
            Send($"R{level}\n");
        }

        public override void Pulse(int level, int ms) => Send($"P{Mathf.Clamp(level, 0, 255)},{Mathf.Clamp(ms, 0, 2000)}\n");

        public override void SetLeds(bool green, bool red)
        {
            string cmd = $"L{(green ? 1 : 0)},{(red ? 1 : 0)}\n";
            if (cmd == lastLeds) return;
            lastLeds = cmd;
            Send(cmd);
        }

        public override void ResetOrientation() => resetRequested = true;
        /// <summary>Hiệu chuẩn lại con quay: để yên tay cầm khoảng 2 giây.</summary>
        public void CalibrateGyro() => Send("C\n");
        /// <summary>Lấy vị trí nghỉ hiện tại của hai joystick làm tâm: buông tay khỏi joystick.</summary>
        public void CenterJoysticks() => Send("J\n");
        public void RequestInfo() => Send("I\n");

        void Send(string cmd)
        {
            var c = client;
            if (c == null) return;
            IPEndPoint to;
            lock (gate) to = remote ?? fixedRemote;
            if (to == null) return;
            var bytes = Encoding.ASCII.GetBytes(cmd);
            try { c.Send(bytes, bytes.Length, to); }
            catch (Exception) { }  // mạng rớt, tay cầm tắt: bỏ qua, nhịp tim sau gửi lại
        }

        bool OpenSocket()
        {
            try
            {
                var c = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort)) { EnableBroadcast = true };
                c.Client.ReceiveTimeout = 200;
                // Windows: sau khi gửi tới tay cầm đã tắt, lần nhận kế tiếp báo lỗi WSAECONNRESET; tắt hành vi đó (SIO_UDP_CONNRESET)
                try { c.Client.IOControl(-1744830452, new byte[] { 0 }, null); }
                catch (Exception) { }
                client = c;
                portError = "";
                return true;
            }
            catch (Exception e)
            {
                portError = e.Message;
                return false;
            }
        }

        void ReceiveLoop()
        {
            var packets = new List<RawPacket>();
            uint lastT = 0;
            bool haveT = false, warned = false;
            while (running)
            {
                if (client == null && !OpenSocket())
                {
                    if (!warned) Debug.LogWarning($"[Tay cầm Wi-Fi] Không mở được cổng UDP {listenPort}: {portError}. Thử lại mỗi 2 s.");
                    warned = true;
                    Thread.Sleep(2000);
                    continue;
                }
                byte[] data;
                var from = new IPEndPoint(IPAddress.Any, 0);
                try { data = client.Receive(ref from); }
                catch (SocketException) { continue; }  // hết thời gian chờ, hoặc lỗi mạng tạm thời
                catch (ObjectDisposedException) { break; }
                catch (NullReferenceException) { break; }

                double now = Clock.Elapsed.TotalSeconds;
                lock (gate)
                {
                    bool same = remote != null && remote.Equals(from);
                    // Đang nối một tay cầm thì bỏ qua tay cầm khác trong phòng, trừ khi tay cầm cũ im quá 1 s
                    if (!same && remote != null && now - lastHeardS < 1.0) continue;
                    if (!same)
                    {
                        remote = from;
                        parser = new PacketParser();
                        haveT = false;
                        resetRequested = true;
                        xyb.Reset();  // tay cầm mới: lấy lại tâm cần
                        textFormat = false;
                        newRemote = true;
                    }
                    lastHeardS = now;
                }

                // Dòng chữ "X,Y,B" (bộ joystick → Mega → ESP32), không phải gói nhị phân hay thông báo '#'
                if (data.Length > 0 && data[0] != 0xA5 && data[0] != (byte)'#')
                {
                    foreach (var line in Encoding.ASCII.GetString(data).Split('\n'))
                    {
                        if (XybDecoder.TryParse(line, out int x, out int y, out bool b))
                        {
                            textFormat = true;
                            var s = xyb.ToState(x, y, b, joystickDeadzone, invertJoy2X, invertJoy2Y, swapJoy2Axes);
                            s.TimeMs = (uint)Clock.ElapsedMilliseconds;
                            samples.Enqueue(s);
                        }
                        else if (line.Trim() == "NO_MEGA_DATA")
                        {
                            textFormat = true;
                            noMegaFlag = true;
                        }
                    }
                    continue;
                }

                packets.Clear();
                parser.Feed(data, data.Length, packets);
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
        }
    }
}
