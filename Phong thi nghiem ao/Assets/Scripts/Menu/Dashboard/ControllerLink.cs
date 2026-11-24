using Khkt.Input;
using UnityEngine;

namespace Khkt.Menu.Dashboard
{
    /// <summary>
    /// Kết nối tay cầm dùng chung cho cả sảnh: thanh trạng thái ở góc trên và trang "Tay cầm" cùng đọc từ đây.
    ///  • Nguồn USB (SerialControllerSource, tự dò cổng) và Wi-Fi (UdpControllerSource, cổng 4210) tạo khi sảnh hiện,
    ///    huỷ khi sảnh ẩn (Release) để vào lab không bị giữ cổng COM / UDP.
    ///  • Sân khấu 3D riêng (xa cảnh chính) có mô hình thật VERA Band 2 ×1.3 (VeraModel), quay theo hướng tay cầm đã bù
    ///    phần căn trục BMI160 (VeraMount, giống tia trỏ trong lab): q = q_mạch ⊗ Mount, đổi sang Unity bằng Frames.ToUnity.
    ///    Hai máy quay vẽ vào 2 ảnh: ảnh nhỏ cho thanh góc, ảnh lớn cho trang Tay cầm.
    ///  • Yaw so với lúc "Đặt hướng về 0" (lần đầu có dữ liệu tự đặt), roll / pitch theo trọng lực.
    /// </summary>
    public class ControllerLink : MonoBehaviour
    {
        public SerialControllerSource Usb { get; private set; }
        public UdpControllerSource Wifi { get; private set; }
        public ControllerSource Active { get; private set; }
        public bool UsbLive { get; private set; }
        public bool WifiLive { get; private set; }
        public ControllerState Latest { get; private set; }
        /// <summary>Hướng tay cầm (Unity) đã bù căn trục và trừ yaw gốc: mũi +z, cán +y.</summary>
        public Quaternion Pose { get; private set; } = Quaternion.identity;
        public Quaternion Mount { get; private set; } = Quaternion.identity;
        public bool MountCalibrated { get; private set; }
        public VeraModel Model { get; private set; }
        public RenderTexture SmallView => rtSmall;
        public RenderTexture LargeView => rtLarge;
        /// <summary>Trang Tay cầm đang mở: bật máy quay ảnh lớn.</summary>
        public bool LargeViewWanted { get; set; }
        public int PacketsPerSecond => Active == Usb && Usb != null ? Usb.PacketsPerSecond : Active == Wifi && Wifi != null ? Wifi.PacketsPerSecond : 0;
        public string FirmwareMessage => UsbLive ? Usb.LastFirmwareMessage : WifiLive ? Wifi.LastFirmwareMessage : "";
        public string ConnectionName => Active == null ? "" : Active == Usb ? "USB " + Usb.ControllerName : "Wi-Fi " + Wifi.ControllerName;
        /// <summary>Trục joystick đang nhảy loạn (dây VRx / VRy hở): "", "X", "Y" hoặc "X, Y".</summary>
        public string JoyNoisyAxes { get; private set; } = "";
        readonly PadInput.JoyFlapDetector flapX = new PadInput.JoyFlapDetector(), flapY = new PadInput.JoyFlapDetector();
        /// <summary>Có nguồn riêng (false: nguồn do nơi khác tạo, ví dụ lab đang mở).</summary>
        public bool OwnsSources => host != null;

        GameObject host;
        float usbLast = -10f, wifiLast = -10f;
        float zeroYaw;
        bool zeroSet;
        float liveSince = -1f;
        bool noisyX, noisyY;  // bắt đầu có dữ liệu liên tục (gốc yaw lấy sau 0,5 s: gói đầu sau khi mở cổng chưa có hướng thật)
        GameObject stage;
        Camera camSmall, camLarge;
        RenderTexture rtSmall, rtLarge;

        void OnEnable() => Acquire();
        void OnDisable() => Release();

        void OnDestroy()
        {
            Release();
            if (stage != null) Destroy(stage);
            if (rtSmall != null) { rtSmall.Release(); Destroy(rtSmall); }
            if (rtLarge != null) { rtLarge.Release(); Destroy(rtLarge); }
        }

        /// <summary>Mở nguồn tay cầm (gọi khi sảnh hiện).</summary>
        public void Acquire()
        {
            if (Usb != null || Wifi != null) return;
            Mount = VeraMount.Load();
            MountCalibrated = Mount != Quaternion.identity;
            if (FindAnyObjectByType<SerialControllerSource>() == null)
            {
                host = new GameObject("Tay cầm (sảnh)");
                Usb = host.AddComponent<SerialControllerSource>();
                Usb.portName = "auto";
                Usb.logFirmwareMessages = false;
                if (FindAnyObjectByType<UdpControllerSource>() == null)
                {
                    Wifi = host.AddComponent<UdpControllerSource>();
                    Wifi.logFirmwareMessages = false;
                }
            }
            else
            {
                Usb = FindAnyObjectByType<SerialControllerSource>();
                Wifi = FindAnyObjectByType<UdpControllerSource>();
            }
            if (Usb != null) Usb.SampleReceived += OnUsb;
            if (Wifi != null) Wifi.SampleReceived += OnWifi;
            if (stage != null) stage.SetActive(true);
        }

        /// <summary>Trả cổng COM / UDP (gọi ngay khi rời sảnh, trước khi lab mở nguồn của nó).</summary>
        public void Release()
        {
            if (Usb != null) Usb.SampleReceived -= OnUsb;
            if (Wifi != null) Wifi.SampleReceived -= OnWifi;
            if (host != null) Destroy(host);
            host = null;
            Usb = null;
            Wifi = null;
            Active = null;
            UsbLive = WifiLive = false;
            if (stage != null) stage.SetActive(false);
        }

        void OnUsb(ControllerState s) => usbLast = Time.unscaledTime;
        void OnWifi(ControllerState s) => wifiLast = Time.unscaledTime;

        public void Send(string cmd)
        {
            if (Active == Usb && Usb != null) Usb.SendCommand(cmd);
            else if (Active == Wifi && Wifi != null) Wifi.SendCommand(cmd);
        }

        public void SetZero()
        {
            zeroSet = false;  // khung sau lấy yaw hiện tại làm gốc
            Send("z");
        }

        public void SetMount(Quaternion m)
        {
            Mount = m;
            MountCalibrated = m != Quaternion.identity;
            zeroSet = false;
        }

        /// <summary>Hướng tay cầm trong Unity đã bù căn trục, CHƯA trừ yaw gốc.</summary>
        public Quaternion WorldPose(in ControllerState s) => Frames.ToUnity(s.OrientationRH * Mount);

        void EnsureStage()
        {
            if (stage != null) return;
            stage = new GameObject("Sân khấu tay cầm 3D (sảnh)");
            stage.transform.position = new Vector3(0, -1000, 0);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Model = VeraModel.Build(stage.transform, shader);
            Model.Root.localPosition = -Model.Bounds.center;
            var pivot = new GameObject("Tâm xoay").transform;
            pivot.SetParent(stage.transform, false);
            Model.Root.SetParent(pivot, false);

            void L(Vector3 pos, float intensity, Color c)
            {
                var go = new GameObject("Đèn");
                go.transform.SetParent(stage.transform, false);
                go.transform.localPosition = pos;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 2f;
                l.intensity = intensity;
                l.color = c;
            }
            L(new Vector3(0.25f, 0.35f, -0.35f), 2.6f, Color.white);
            L(new Vector3(-0.35f, 0.05f, -0.15f), 1.2f, new Color(0.85f, 0.9f, 1f));
            L(new Vector3(0f, 0.3f, 0.35f), 1.0f, Color.white);

            Camera Cam(string name, Vector3 pos, float fov, RenderTexture rt)
            {
                var go = new GameObject(name);
                go.transform.SetParent(stage.transform, false);
                go.transform.localPosition = pos;
                go.transform.LookAt(stage.transform.position + new Vector3(0, 0.01f, 0));
                var c = go.AddComponent<Camera>();
                c.fieldOfView = fov;
                c.nearClipPlane = 0.05f;
                c.farClipPlane = 3f;
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = MenuKit.PaperAlt;
                c.targetTexture = rt;
                c.enabled = false;
                return c;
            }
            // nhìn từ sau lưng người cầm, hơi cao và lệch phải: mũi tay cầm hướng ra xa người xem, quay theo tay
            rtSmall = new RenderTexture(176, 176, 16) { name = "Tay cầm 3D nhỏ", antiAliasing = 4 };
            rtLarge = new RenderTexture(840, 520, 16) { name = "Tay cầm 3D", antiAliasing = 4 };
            camSmall = Cam("Máy quay nhỏ", new Vector3(0.12f, 0.16f, -0.34f), 34, rtSmall);
            camLarge = Cam("Máy quay lớn", new Vector3(0.11f, 0.12f, -0.33f), 30, rtLarge);
            camSmall.backgroundColor = MenuKit.Paper;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            UsbLive = Usb != null && now - usbLast < 0.6f;
            WifiLive = Wifi != null && now - wifiLast < 0.6f;
            Active = UsbLive ? (ControllerSource)Usb : WifiLive ? Wifi : null;
            if (Usb == null && Wifi == null) return;
            EnsureStage();
            if (Active == null)
            {
                liveSince = -1f;
                zeroSet = false;
                camSmall.enabled = false;
                camLarge.enabled = LargeViewWanted;
                return;
            }
            Latest = Active.Latest;
            var s = Latest;
            bool nx = flapX.Step(s.JoyRaw.x, now) || flapX.QuietFor(now) < 3f && flapX.QuietFor(now) >= 0f && noisyX;
            bool ny = flapY.Step(s.JoyRaw.y, now) || flapY.QuietFor(now) < 3f && flapY.QuietFor(now) >= 0f && noisyY;
            noisyX = nx;
            noisyY = ny;
            JoyNoisyAxes = nx && ny ? "X, Y" : nx ? "X" : ny ? "Y" : "";
            var q = WorldPose(s);
            if (liveSince < 0) liveSince = now;
            if (!zeroSet && now - liveSince >= 0.5f && !s.Has(VeraFlags.ImuError))
            {
                Vector3 f = q * Vector3.forward;
                zeroYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                zeroSet = true;
            }
            Pose = Quaternion.Euler(0, -zeroYaw, 0) * q;
            Model.Root.parent.localRotation = Pose;
            Model.Animate(s, Time.unscaledDeltaTime);
            camSmall.enabled = true;
            camLarge.enabled = LargeViewWanted;
        }
    }
}
