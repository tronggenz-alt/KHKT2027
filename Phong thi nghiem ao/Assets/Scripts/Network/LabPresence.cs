using System.Collections.Generic;
using System.Text;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Menu;
using Khkt.Rendering;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Network
{
    /// <summary>
    /// Thấy nhau trong phòng lab: vị trí đầu, tay của từng người và vật đang được cầm. Máy khách gửi cho
    /// máy chủ phòng, máy chủ kiểm tra rồi chuyển cho các máy còn lại. Một vật chỉ một người cầm: máy chủ
    /// quyết định ai được, người đến sau bị từ chối.
    /// Chưa đồng bộ dung dịch, phản ứng, chỗ đặt trong giá: đây là bước thử trước khi làm toàn bộ thí nghiệm.
    /// </summary>
    public class LabPresence : MonoBehaviour
    {
        class Remote
        {
            public string name = "";
            public GameObject root;
            public Transform head, body, hand;
            public Transform label;
            public Text labelText;
            public Vector3 headPos, handPos;
            public float yaw, pitch;
            public Quaternion handRot = Quaternion.identity;
            public bool seen;
        }

        struct Pose
        {
            public Vector3 pos;
            public Quaternion rot;
        }

        public static LabPresence Instance { get; private set; }

        const string MsgPose = "vera.vitri", MsgHello = "vera.chao", MsgName = "vera.ten", MsgGone = "vera.roi";
        const string MsgAsk = "vera.xin_cam", MsgOwner = "vera.cam", MsgDenied = "vera.tu_choi", MsgObject = "vera.vat", MsgRelease = "vera.tha";
        const float SendInterval = 1f / 15f;
        const ulong NoOwner = ulong.MaxValue;

        public int RemoteCount => remotes.Count;
        public long PayloadBytesSent { get; private set; }
        public long PayloadBytesReceived { get; private set; }

        readonly Dictionary<ulong, Remote> remotes = new Dictionary<ulong, Remote>();
        readonly Dictionary<ulong, string> names = new Dictionary<ulong, string>();
        readonly Dictionary<int, Grabbable> byId = new Dictionary<int, Grabbable>();
        readonly Dictionary<Grabbable, int> idOf = new Dictionary<Grabbable, int>();
        readonly Dictionary<int, ulong> owners = new Dictionary<int, ulong>();
        readonly Dictionary<int, Pose> lastPose = new Dictionary<int, Pose>();
        readonly HashSet<int> remoteHeld = new HashSet<int>();

        NetworkManager net;
        CustomMessagingManager msg;
        RoomConnection room;
        DesktopViewer viewer;
        HandController hand;
        Grabbable localHeld;
        int localHeldId;
        float nextSend, nextHello, nextHud;
        bool welcomed;
        Text hud;
        Material bodyMat, handMat;

        ulong Me => net.LocalClientId;

        // =====================================================================
        // Vòng đời
        // =====================================================================

        void Start()
        {
            Instance = this;
            room = RoomConnection.Instance;
            net = NetworkManager.Singleton;
            msg = net.CustomMessagingManager;
            viewer = FindAnyObjectByType<DesktopViewer>();
            hand = HandController.Active != null ? HandController.Active : FindAnyObjectByType<HandController>();

            msg.RegisterNamedMessageHandler(MsgPose, OnPose);
            msg.RegisterNamedMessageHandler(MsgHello, OnHello);
            msg.RegisterNamedMessageHandler(MsgName, OnName);
            msg.RegisterNamedMessageHandler(MsgGone, OnGone);
            msg.RegisterNamedMessageHandler(MsgAsk, OnAsk);
            msg.RegisterNamedMessageHandler(MsgOwner, OnOwner);
            msg.RegisterNamedMessageHandler(MsgDenied, OnDenied);
            msg.RegisterNamedMessageHandler(MsgObject, OnObject);
            msg.RegisterNamedMessageHandler(MsgRelease, OnRelease);
            ZoneStreamer.ZoneChanged += OnZoneChanged;
            room.Lost += OnRoomLost;
            if (net.IsServer) net.OnClientDisconnectCallback += OnClientLeft;

            names[Me] = GameSession.DisplayName;
            welcomed = net.IsServer;
            ScanObjects();
            BuildHud();
            LabMessages.Say(net.IsServer ? $"Đã mở phòng. Mã vào phòng: {room.Code}" : "Đã vào phòng học chung", LabMessages.Tone.Info);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ZoneStreamer.ZoneChanged -= OnZoneChanged;
            if (room != null) room.Lost -= OnRoomLost;
            if (net != null) net.OnClientDisconnectCallback -= OnClientLeft;
            if (msg != null && net != null && net.IsListening)
            {
                foreach (var name in new[] { MsgPose, MsgHello, MsgName, MsgGone, MsgAsk, MsgOwner, MsgDenied, MsgObject, MsgRelease })
                    msg.UnregisterNamedMessageHandler(name);
            }
            foreach (var r in remotes.Values) if (r.root != null) Destroy(r.root);
            if (hud != null) Destroy(hud.transform.parent.gameObject);
        }

        void OnRoomLost()
        {
            LabMessages.Say("Máy mở phòng đã đóng phòng: bạn vẫn làm tiếp một mình được", LabMessages.Tone.Warn);
            // trả lại những vật đang bị khóa vì người khác cầm
            foreach (int id in remoteHeld)
                if (byId.TryGetValue(id, out var g) && g != null) g.SetHeld(false);
            remoteHeld.Clear();
            Destroy(gameObject);
        }

        void Update()
        {
            if (net == null || !net.IsListening) return;
            float now = Time.realtimeSinceStartup;
            if (!welcomed && now >= nextHello)
            {
                // máy chủ có thể chưa tải xong lab: chào lại tới khi được trả lời
                nextHello = now + 1f;
                SendName();
            }
            WatchHeld();
            if (now >= nextSend)
            {
                nextSend = now + SendInterval;
                SendPose();
                if (localHeld != null && localHeldId != 0) SendObject(localHeldId, localHeld.transform);
            }
            MoveRemotes();
            MoveRemoteObjects();
            if (now >= nextHud)
            {
                nextHud = now + 0.5f;
                RefreshHud();
            }
        }

        // =====================================================================
        // Gửi, nhận chung
        // =====================================================================

        void ToServer(string name, FastBufferWriter w, NetworkDelivery d)
        {
            PayloadBytesSent += w.Length;
            msg.SendNamedMessage(name, NetworkManager.ServerClientId, w, d);
        }

        /// <summary>Máy chủ gửi cho mọi máy khách, trừ người vừa gửi tin gốc.</summary>
        void ToClients(string name, FastBufferWriter w, NetworkDelivery d, ulong except)
        {
            foreach (ulong id in net.ConnectedClientsIds)
            {
                if (id == NetworkManager.ServerClientId || id == except) continue;
                PayloadBytesSent += w.Length;
                msg.SendNamedMessage(name, id, w, d);
            }
        }

        static FastBufferWriter NewWriter(int size = 96) => new FastBufferWriter(size, Allocator.Temp, 1024);

        // =====================================================================
        // Người trong phòng
        // =====================================================================

        void SendName()
        {
            using (var w = NewWriter())
            {
                w.WriteValueSafe(Me);
                w.WriteValueSafe(GameSession.DisplayName);
                ToServer(MsgName, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnName(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong id);
            r.ReadValueSafe(out string name);
            if (net.IsServer)
            {
                if (id != sender) return;
                bool first = !names.ContainsKey(id);
                names[id] = name;
                if (first)
                {
                    LabMessages.Say($"{name} đã vào phòng", LabMessages.Tone.Info);
                    using (var w = NewWriter())
                    {
                        w.WriteValueSafe(id);
                        w.WriteValueSafe(name);
                        ToClients(MsgName, w, NetworkDelivery.ReliableSequenced, id);
                    }
                }
                SendWorld(id);
                return;
            }
            names[id] = name;
            if (remotes.TryGetValue(id, out var remote)) SetName(remote, name);
        }

        /// <summary>Người đến sau: tên mọi người, ai đang cầm gì, vật đã bị dời tới đâu.</summary>
        void SendWorld(ulong to)
        {
            foreach (var kv in names)
            {
                if (kv.Key == to) continue;
                using (var w = NewWriter())
                {
                    w.WriteValueSafe(kv.Key);
                    w.WriteValueSafe(kv.Value);
                    PayloadBytesSent += w.Length;
                    msg.SendNamedMessage(MsgName, to, w, NetworkDelivery.ReliableSequenced);
                }
            }
            foreach (var kv in lastPose)
            {
                if (owners.ContainsKey(kv.Key)) continue;
                using (var w = NewWriter())
                {
                    WriteObject(w, Me, kv.Key, kv.Value.pos, kv.Value.rot);
                    PayloadBytesSent += w.Length;
                    msg.SendNamedMessage(MsgRelease, to, w, NetworkDelivery.ReliableSequenced);
                }
            }
            foreach (var kv in owners)
            {
                using (var w = NewWriter())
                {
                    w.WriteValueSafe(kv.Key);
                    w.WriteValueSafe(kv.Value);
                    PayloadBytesSent += w.Length;
                    msg.SendNamedMessage(MsgOwner, to, w, NetworkDelivery.ReliableSequenced);
                }
            }
            using (var w = NewWriter())
            {
                w.WriteValueSafe(true);
                PayloadBytesSent += w.Length;
                msg.SendNamedMessage(MsgHello, to, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnHello(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            welcomed = true;
        }

        void OnGone(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong id);
            RemoveRemote(id);
        }

        void OnClientLeft(ulong id)
        {
            if (id == NetworkManager.ServerClientId) return;
            if (names.TryGetValue(id, out string name)) LabMessages.Say($"{name} đã rời phòng", LabMessages.Tone.Info);
            names.Remove(id);
            RemoveRemote(id);
            using (var w = NewWriter())
            {
                w.WriteValueSafe(id);
                ToClients(MsgGone, w, NetworkDelivery.ReliableSequenced, id);
            }
            // người rời phòng đang cầm gì thì thả tại chỗ cuối cùng
            var held = new List<int>();
            foreach (var kv in owners) if (kv.Value == id) held.Add(kv.Key);
            foreach (int obj in held)
            {
                var t = byId.TryGetValue(obj, out var g) && g != null ? g.transform : null;
                Pose p = lastPose.TryGetValue(obj, out var known) ? known : new Pose { pos = t != null ? t.position : Vector3.zero, rot = t != null ? t.rotation : Quaternion.identity };
                ReleaseOnServer(obj, p.pos, p.rot, Me);
            }
        }

        void RemoveRemote(ulong id)
        {
            if (!remotes.TryGetValue(id, out var r)) return;
            if (r.root != null) Destroy(r.root);
            remotes.Remove(id);
        }

        void SendPose()
        {
            if (viewer == null || viewer.cameraTransform == null) return;
            var cam = viewer.cameraTransform;
            Vector3 handPos = hand != null && hand.hand != null ? hand.hand.transform.position : cam.position + cam.forward * 0.4f;
            Quaternion handRot = hand != null && hand.hand != null ? hand.hand.transform.rotation : cam.rotation;
            float yaw = viewer.transform.eulerAngles.y, pitch = viewer.Pitch;
            using (var w = NewWriter())
            {
                WritePose(w, Me, cam.position, yaw, pitch, handPos, handRot);
                if (net.IsServer) ToClients(MsgPose, w, NetworkDelivery.UnreliableSequenced, Me);
                else ToServer(MsgPose, w, NetworkDelivery.UnreliableSequenced);
            }
        }

        static void WritePose(FastBufferWriter w, ulong who, Vector3 head, float yaw, float pitch, Vector3 handPos, Quaternion handRot)
        {
            w.WriteValueSafe(who);
            w.WriteValueSafe(head);
            w.WriteValueSafe(yaw);
            w.WriteValueSafe(pitch);
            w.WriteValueSafe(handPos);
            w.WriteValueSafe(handRot);
        }

        void OnPose(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong who);
            r.ReadValueSafe(out Vector3 head);
            r.ReadValueSafe(out float yaw);
            r.ReadValueSafe(out float pitch);
            r.ReadValueSafe(out Vector3 handPos);
            r.ReadValueSafe(out Quaternion handRot);
            if (net.IsServer)
            {
                if (who != sender) return;
                using (var w = NewWriter())
                {
                    WritePose(w, who, head, yaw, pitch, handPos, handRot);
                    ToClients(MsgPose, w, NetworkDelivery.UnreliableSequenced, who);
                }
            }
            if (who == Me) return;
            if (!remotes.TryGetValue(who, out var remote))
            {
                remote = MakeRemote(who);
                remotes[who] = remote;
            }
            remote.headPos = head;
            remote.yaw = yaw;
            remote.pitch = pitch;
            remote.handPos = handPos;
            remote.handRot = handRot;
            if (!remote.seen)
            {
                remote.seen = true;
                remote.head.position = head;
                remote.hand.position = handPos;
            }
        }

        void MoveRemotes()
        {
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            Transform camT = viewer != null ? viewer.cameraTransform : null;
            foreach (var r in remotes.Values)
            {
                if (r.root == null) continue;
                // dịch chuyển trạm thì nhảy luôn, không trôi xuyên tường
                r.head.position = (r.head.position - r.headPos).sqrMagnitude > 9f ? r.headPos : Vector3.Lerp(r.head.position, r.headPos, k);
                r.head.rotation = Quaternion.Slerp(r.head.rotation, Quaternion.Euler(r.pitch, r.yaw, 0), k);
                r.hand.position = (r.hand.position - r.handPos).sqrMagnitude > 9f ? r.handPos : Vector3.Lerp(r.hand.position, r.handPos, k);
                r.hand.rotation = Quaternion.Slerp(r.hand.rotation, r.handRot, k);
                r.body.SetPositionAndRotation(r.head.position + Vector3.down * 0.72f, Quaternion.Euler(0, r.yaw, 0));
                r.label.position = r.head.position + Vector3.up * 0.26f;
                if (camT != null) r.label.rotation = Quaternion.LookRotation(r.label.position - camT.position);
            }
        }

        // =====================================================================
        // Vật đang cầm
        // =====================================================================

        /// <summary>Mã vật: đường dẫn trong cảnh lúc mới tải, giống nhau trên mọi máy vì cùng một bản dựng.</summary>
        void ScanObjects()
        {
            foreach (var g in ZoneRoot.FindAll<Grabbable>())
            {
                if (g == null || idOf.ContainsKey(g)) continue;
                int id = StableId(g.transform);
                if (byId.TryGetValue(id, out var old) && old != null && old != g) continue;
                byId[id] = g;
                idOf[g] = id;
                if (lastPose.TryGetValue(id, out var p)) g.transform.SetPositionAndRotation(p.pos, p.rot);
                if (owners.TryGetValue(id, out ulong owner) && owner != Me) MarkRemoteHeld(id, g);
            }
        }

        static int StableId(Transform t)
        {
            var sb = new StringBuilder(t.gameObject.scene.name);
            var chain = new List<Transform>();
            for (var p = t; p != null; p = p.parent) chain.Add(p);
            for (int i = chain.Count - 1; i >= 0; i--) sb.Append('/').Append(chain[i].GetSiblingIndex());
            sb.Append('/').Append(t.name);
            // FNV-1a: hash của string đổi theo tiến trình nên không dùng GetHashCode
            uint h = 2166136261;
            foreach (char c in sb.ToString()) h = (h ^ c) * 16777619;
            return (int)(h | 1);
        }

        void OnZoneChanged(int zone, bool loaded)
        {
            if (loaded) ScanObjects();
        }

        void WatchHeld()
        {
            var now = hand != null ? hand.Held : null;
            if (now == localHeld) return;
            if (localHeld != null && localHeldId != 0) SendRelease(localHeldId, localHeld.transform);
            localHeld = now;
            localHeldId = 0;
            if (now == null) return;
            if (!idOf.TryGetValue(now, out localHeldId)) ScanObjects();
            if (!idOf.TryGetValue(now, out localHeldId)) return;  // vật sinh lúc chạy: chưa đồng bộ
            if (net.IsServer) AskOnServer(localHeldId, Me);
            else
            {
                using (var w = NewWriter())
                {
                    w.WriteValueSafe(localHeldId);
                    ToServer(MsgAsk, w, NetworkDelivery.ReliableSequenced);
                }
            }
        }

        void OnAsk(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out int id);
            if (net.IsServer) AskOnServer(id, sender);
        }

        void AskOnServer(int id, ulong who)
        {
            if (owners.TryGetValue(id, out ulong owner) && owner != who)
            {
                if (who == Me) Denied(id);
                else
                {
                    using (var w = NewWriter())
                    {
                        w.WriteValueSafe(id);
                        PayloadBytesSent += w.Length;
                        msg.SendNamedMessage(MsgDenied, who, w, NetworkDelivery.ReliableSequenced);
                    }
                }
                return;
            }
            ApplyOwner(id, who);
            using (var w = NewWriter())
            {
                w.WriteValueSafe(id);
                w.WriteValueSafe(who);
                ToClients(MsgOwner, w, NetworkDelivery.ReliableSequenced, ulong.MaxValue);
            }
        }

        void OnOwner(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out ulong who);
            ApplyOwner(id, who);
        }

        void ApplyOwner(int id, ulong who)
        {
            owners[id] = who;
            if (who != Me && byId.TryGetValue(id, out var g) && g != null) MarkRemoteHeld(id, g);
        }

        void MarkRemoteHeld(int id, Grabbable g)
        {
            // SetHeld tắt va chạm: người khác đang cầm thì tia của mình xuyên qua, không chọn được
            if (remoteHeld.Add(id) && !g.IsHeld) g.SetHeld(true);
        }

        void OnDenied(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out int id);
            Denied(id);
        }

        void Denied(int id)
        {
            LabMessages.Say("Bạn khác đang cầm vật này", LabMessages.Tone.Warn);
            if (localHeldId != id || localHeld == null) return;
            var g = localHeld;
            localHeld = null;
            localHeldId = 0;
            hand.ReturnHome();
            if (owners.TryGetValue(id, out ulong owner) && owner != Me)
            {
                remoteHeld.Remove(id);
                MarkRemoteHeld(id, g);
            }
        }

        static void WriteObject(FastBufferWriter w, ulong who, int id, Vector3 pos, Quaternion rot)
        {
            w.WriteValueSafe(who);
            w.WriteValueSafe(id);
            w.WriteValueSafe(pos);
            w.WriteValueSafe(rot);
        }

        void SendObject(int id, Transform t)
        {
            if (owners.TryGetValue(id, out ulong owner) && owner != Me) return;
            using (var w = NewWriter())
            {
                WriteObject(w, Me, id, t.position, t.rotation);
                if (net.IsServer) ToClients(MsgObject, w, NetworkDelivery.UnreliableSequenced, Me);
                else ToServer(MsgObject, w, NetworkDelivery.UnreliableSequenced);
            }
        }

        void OnObject(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong who);
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out Vector3 pos);
            r.ReadValueSafe(out Quaternion rot);
            if (net.IsServer)
            {
                // chỉ người được máy chủ cho cầm mới được dời vật
                if (who != sender || !owners.TryGetValue(id, out ulong owner) || owner != who) return;
                using (var w = NewWriter())
                {
                    WriteObject(w, who, id, pos, rot);
                    ToClients(MsgObject, w, NetworkDelivery.UnreliableSequenced, who);
                }
            }
            if (who != Me) lastPose[id] = new Pose { pos = pos, rot = rot };
        }

        void MoveRemoteObjects()
        {
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            foreach (int id in remoteHeld)
            {
                if (!byId.TryGetValue(id, out var g) || g == null || !lastPose.TryGetValue(id, out var p)) continue;
                var t = g.transform;
                t.SetPositionAndRotation(Vector3.Lerp(t.position, p.pos, k), Quaternion.Slerp(t.rotation, p.rot, k));
            }
        }

        void SendRelease(int id, Transform t)
        {
            lastPose[id] = new Pose { pos = t.position, rot = t.rotation };
            if (net.IsServer)
            {
                ReleaseOnServer(id, t.position, t.rotation, Me);
                return;
            }
            owners.Remove(id);
            using (var w = NewWriter())
            {
                WriteObject(w, Me, id, t.position, t.rotation);
                ToServer(MsgRelease, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnRelease(ulong sender, FastBufferReader r)
        {
            PayloadBytesReceived += r.Length;
            r.ReadValueSafe(out ulong who);
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out Vector3 pos);
            r.ReadValueSafe(out Quaternion rot);
            if (net.IsServer)
            {
                if (who != sender || !owners.TryGetValue(id, out ulong owner) || owner != who) return;
                ReleaseOnServer(id, pos, rot, who);
                return;
            }
            ApplyRelease(id, pos, rot, who);
        }

        void ReleaseOnServer(int id, Vector3 pos, Quaternion rot, ulong who)
        {
            ApplyRelease(id, pos, rot, who);
            using (var w = NewWriter())
            {
                WriteObject(w, who, id, pos, rot);
                ToClients(MsgRelease, w, NetworkDelivery.ReliableSequenced, who == Me ? ulong.MaxValue : who);
            }
        }

        void ApplyRelease(int id, Vector3 pos, Quaternion rot, ulong who)
        {
            owners.Remove(id);
            lastPose[id] = new Pose { pos = pos, rot = rot };
            if (who == Me) return;
            if (!byId.TryGetValue(id, out var g) || g == null) return;
            if (remoteHeld.Remove(id)) g.SetHeld(false);
            g.transform.SetPositionAndRotation(pos, rot);
        }

        // =====================================================================
        // Hình người, bảng trạng thái
        // =====================================================================

        Remote MakeRemote(ulong id)
        {
            if (bodyMat == null)
            {
                bodyMat = Lit(new Color(0.35f, 0.52f, 0.72f));
                handMat = Lit(new Color(0.27f, 0.56f, 0.93f));
            }
            var r = new Remote { root = new GameObject("Người cùng phòng " + id) };
            r.body = Part(PrimitiveType.Capsule, r.root.transform, "Thân", new Vector3(0.36f, 0.56f, 0.24f), bodyMat);
            r.head = Part(PrimitiveType.Sphere, r.root.transform, "Đầu", Vector3.one * 0.21f, bodyMat);
            // Tấm che mặt cho biết người đó đang nhìn hướng nào
            var visor = Part(PrimitiveType.Cube, r.head, "Mặt", new Vector3(0.75f, 0.28f, 0.2f), handMat);
            visor.localPosition = new Vector3(0, 0.08f, 0.44f);
            r.hand = Part(PrimitiveType.Sphere, r.root.transform, "Tay", new Vector3(0.09f, 0.04f, 0.13f), handMat);

            var labelGo = new GameObject("Tên", typeof(Canvas));
            labelGo.transform.SetParent(r.root.transform, false);
            var canvas = labelGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)labelGo.transform;
            rt.sizeDelta = new Vector2(520, 72);
            rt.localScale = Vector3.one * 0.0016f;
            var bg = GlassKit.Rect(rt, "Nền", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 72)).gameObject.AddComponent<Image>();
            bg.sprite = GlassKit.Solid;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.04f, 0.06f, 0.1f, 0.78f);
            bg.raycastTarget = false;
            r.labelText = GlassKit.Label(rt, "Chữ", "", 42, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(520, 72), new Vector2(0, 1));
            r.label = labelGo.transform;
            SetName(r, names.TryGetValue(id, out string name) ? name : "Bạn học");
            return r;
        }

        static void SetName(Remote r, string name)
        {
            r.name = name;
            if (r.labelText != null) r.labelText.text = name;
        }

        static Material Lit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.35f);
            return m;
        }

        static Transform Part(PrimitiveType type, Transform parent, string name, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // không có va chạm: tia chọn vật và người đi xuyên qua hình người khác
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void BuildHud()
        {
            var ui = FindAnyObjectByType<ModernUI>();
            if (ui == null || ui.Canvas == null) return;
            var plate = GlassKit.Panel(ui.Canvas.transform, "Phòng học chung", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(430, 48), null, 3f);
            plate.color = new Color(0.03f, 0.05f, 0.09f, 0.78f);
            plate.sprite = GlassKit.Solid;
            hud = GlassKit.Label(plate.transform, "Chữ", "", 17, Color.white, false, TextAnchor.MiddleLeft, new Vector2(16, 0), new Vector2(404, 48));
            RefreshHud();
        }

        void RefreshHud()
        {
            if (hud == null) return;
            string route = room.ViaRelay ? "Internet" : "LAN";
            hud.text = $"Phòng <b>{room.Code}</b> · {route} · {remotes.Count + 1} người";
        }

        // =====================================================================
        // Cho bài kiểm thử
        // =====================================================================

        public int IdOf(Grabbable g) => g != null && idOf.TryGetValue(g, out int id) ? id : 0;
        public Grabbable ObjectById(int id) => byId.TryGetValue(id, out var g) ? g : null;
        public bool IsRemoteHeld(int id) => remoteHeld.Contains(id);

        public bool RemoteHeadOf(ulong id, out Vector3 pos, out string name)
        {
            pos = Vector3.zero;
            name = "";
            if (!remotes.TryGetValue(id, out var r) || r.root == null) return false;
            pos = r.headPos;
            name = r.name;
            return true;
        }
    }
}
