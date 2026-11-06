using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Khkt.Menu;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khkt.Network
{
    /// <summary>
    /// Giữ kết nối phòng học chung qua các lần đổi cảnh. Máy mở phòng làm máy chủ, nối qua Unity Relay
    /// hoặc trực tiếp trong mạng LAN. Mỗi máy tự tải lab; chỉ có tin nhắn đi qua mạng (LabPresence),
    /// nên không cần NetworkObject trên từng vật và không dùng quản lý cảnh của Netcode.
    /// </summary>
    public class RoomConnection : MonoBehaviour
    {
        public const ushort LanPort = 7777;
        const int MaxRelayPeers = 30;
        const string RelayProtocol = "dtls";

        static RoomConnection instance;

        public static RoomConnection Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Kết nối phòng học chung");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<RoomConnection>();
                }
                return instance;
            }
        }

        public static bool InRoom => instance != null && instance.net != null && instance.net.IsListening;

        /// <summary>Địa chỉ máy mở phòng lắng nghe; mặc định mọi card mạng. Bài thử đặt 127.0.0.1 để Windows không hỏi tường lửa.</summary>
        public static string LanListenAddress { get; set; } = "0.0.0.0";

        public string Code { get; private set; } = "";
        public bool IsHost => net != null && net.IsHost;
        public bool ViaRelay { get; private set; }
        public string Error { get; private set; } = "";
        public event Action Lost;

        NetworkManager net;
        UnityTransport transport;
        bool leaving;

        void Awake() => SceneManager.sceneLoaded += OnSceneLoaded;

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameSession.LabScene && InRoom) new GameObject("Người cùng phòng").AddComponent<LabPresence>();
        }

        // =====================================================================
        // Mở phòng, vào phòng
        // =====================================================================

        public async Task<bool> HostRelay()
        {
            Error = "";
            try
            {
                await SignIn();
                var allocation = await RelayService.Instance.CreateAllocationAsync(MaxRelayPeers);
                string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                CreateManager();
                transport.SetRelayServerData(allocation.ToRelayServerData(RelayProtocol));
                ViaRelay = true;
                Code = code;
                return Begin(true);
            }
            catch (Exception e)
            {
                return Fail("Không mở được phòng qua Internet. Hãy kiểm tra mạng hoặc mở phòng trong mạng LAN.", e);
            }
        }

        public bool HostLan()
        {
            Error = "";
            try
            {
                CreateManager();
                transport.SetConnectionData("127.0.0.1", LanPort, LanListenAddress);
                ViaRelay = false;
                Code = LocalIp();
                return Begin(true);
            }
            catch (Exception e)
            {
                return Fail("Không mở được phòng trong mạng LAN.", e);
            }
        }

        /// <summary>Có dấu chấm là địa chỉ IP trong mạng LAN, còn lại là mã Relay.</summary>
        public async Task<bool> Join(string codeOrIp)
        {
            Error = "";
            string code = codeOrIp.Trim();
            try
            {
                if (code.Contains('.'))
                {
                    CreateManager();
                    transport.SetConnectionData(code, LanPort);
                    ViaRelay = false;
                }
                else
                {
                    code = code.ToUpperInvariant();
                    await SignIn();
                    JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
                    CreateManager();
                    transport.SetRelayServerData(allocation.ToRelayServerData(RelayProtocol));
                    ViaRelay = true;
                }
                Code = code;
                return await WaitConnected();
            }
            catch (RelayServiceException e)
            {
                return Fail("Không tìm thấy phòng với mã này. Kiểm tra lại mã, hoặc phòng đã đóng.", e);
            }
            catch (Exception e)
            {
                return Fail("Không vào được phòng. Hãy kiểm tra kết nối mạng.", e);
            }
        }

        async Task<bool> WaitConnected()
        {
            if (!Begin(false)) return false;
            float until = Time.realtimeSinceStartup + 16f;
            while (net != null && !net.IsConnectedClient && Time.realtimeSinceStartup < until) await Task.Yield();
            if (net != null && net.IsConnectedClient) return true;
            return Fail("Không kết nối được tới máy mở phòng. Kiểm tra địa chỉ, và máy đó đã cho phép qua tường lửa chưa.", null);
        }

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // mỗi cửa sổ một hồ sơ: hai cửa sổ trên cùng máy không dùng chung một người chơi ẩn danh
                var options = new InitializationOptions();
                options.SetProfile("vera" + System.Diagnostics.Process.GetCurrentProcess().Id);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        void CreateManager()
        {
            DestroyManager();
            // Netcode không chạy nếu NetworkManager có đối tượng cha; nó tự giữ qua các lần đổi cảnh
            var go = new GameObject("Mạng phòng học chung");
            go.SetActive(false);
            net = go.AddComponent<NetworkManager>();
            transport = go.AddComponent<UnityTransport>();
            transport.ConnectTimeoutMS = 1000;
            transport.MaxConnectAttempts = 15;
            net.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = false,
                TickRate = 30,
            };
            go.SetActive(true);
            net.OnClientDisconnectCallback += OnDisconnected;
        }

        bool Begin(bool host)
        {
            leaving = false;
            // người khác vẫn cần nhận tin khi cửa sổ này mất tiêu điểm
            Application.runInBackground = true;
            bool ok = host ? net.StartHost() : net.StartClient();
            return ok || Fail(host ? "Không mở được phòng. Có thể cổng mạng đang bận." : "Không kết nối được tới phòng.", null);
        }

        void OnDisconnected(ulong clientId)
        {
            if (net == null || leaving || net.IsServer || clientId != net.LocalClientId) return;
            Lost?.Invoke();
            Leave();
        }

        bool Fail(string message, Exception e)
        {
            Error = message;
            if (e != null) Debug.LogWarning("[VERA] Phòng học chung: " + e.Message);
            Leave();
            return false;
        }

        // =====================================================================
        // Rời phòng
        // =====================================================================

        public void Leave()
        {
            leaving = true;
            DestroyManager();
            Code = "";
        }

        void DestroyManager()
        {
            if (net == null) return;
            net.OnClientDisconnectCallback -= OnDisconnected;
            if (net.IsListening) net.Shutdown();
            Destroy(net.gameObject);
            net = null;
            transport = null;
        }

        /// <summary>IPv4 của card mạng đang chạy, để các máy khác trong LAN nhập vào.</summary>
        static string LocalIp()
        {
            try
            {
                var all = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())
                    .ToList();
                // ưu tiên dải mạng riêng thường gặp ở trường, ở nhà
                return all.FirstOrDefault(a => a.StartsWith("192.168.")) ?? all.FirstOrDefault(a => a.StartsWith("10.")) ?? all.FirstOrDefault() ?? "127.0.0.1";
            }
            catch (Exception)
            {
                return "127.0.0.1";
            }
        }
    }
}
